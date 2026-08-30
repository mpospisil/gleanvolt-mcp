using System.Security.Cryptography;
using System.Text;
using Gleanvolt.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

GleanvoltOptions options;
TransportOptions transport;

try
{
    options = GleanvoltOptions.FromEnvironment();
    transport = TransportOptions.FromEnvironment();
}
catch (InvalidOperationException exception)
{
    // stderr, and a non-zero exit: an MCP client reads stdout as protocol frames, so a message written
    // there would be a parse error rather than an explanation. Under HTTP nothing is reading stdout,
    // but stderr is where a container's logs collect it just the same.
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}

return transport.Kind switch
{
    Transport.Http => await RunHttpAsync(options, transport, args),
    _ => await RunStdioAsync(options, args),
};

// One process per client, launched by the client. The transport this server was written for, and still
// the default: a registration made before HTTP existed launches exactly what it launched then.
static async Task<int> RunStdioAsync(GleanvoltOptions options, string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // Everything the logger emits goes to stderr. The default console provider writes to stdout, which is
    // the protocol stream -- one stray informational line and the client drops the connection.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services
        .AddGleanvoltMcpServer(options, Transport.Stdio)
        .WithStdioServerTransport();

    var host = builder.Build();

    Announce(host.Services, options, endpoint: null);

    await host.RunAsync();
    return 0;
}

// One long-running process, reached over the network by any number of clients at once. This is what
// Home Assistant's `mcp` integration can consume: it is a client that points at a URL and cannot spawn
// a process, so something has to be listening before it is configured.
static async Task<int> RunHttpAsync(GleanvoltOptions options, TransportOptions transport, string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Explicit, so the bind variable is the one thing that decides the address. Left to the defaults,
    // Kestrel would also answer to ASPNETCORE_URLS and to a launch profile, and a container that came up
    // on the wrong port would be a puzzle with three possible causes.
    builder.WebHost.UseUrls(transport.BindAddress.ToString());

    builder.Services
        .AddGleanvoltMcpServer(options, Transport.Http)
        .WithHttpTransport(http =>
        {
            // Home Assistant opens a fresh MCP session for every single tool call and closes it again,
            // and re-lists the tools on a half-hour timer. Session state would be built and thrown away
            // each time. Stateless is both the SDK's default at this protocol revision and the honest
            // description of how this server is used -- pinned here so a default that moves does not
            // quietly start accumulating sessions on a Pi.
            http.Stateless = true;

            // EnableLegacySse is left at its default of false. Streamable HTTP is what Home Assistant
            // tries first, and the /sse and /message pair it would otherwise fall back to accepts POSTs
            // with no backpressure at all -- not something to leave listening on a Pi for a client that
            // does not need it.
        });

    var app = builder.Build();

    // Ahead of the endpoint rather than as an ASP.NET Core authentication scheme: there is one secret
    // and no identity behind it. Read the note on the token variable in the README first -- not every
    // client that can reach this endpoint has any way to send it.
    if (transport.Token is { } expected)
    {
        app.Use(async (context, next) =>
        {
            if (!Presented(context.Request.Headers.Authorization.ToString(), expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";

                await context.Response.WriteAsJsonAsync(new
                {
                    error = $"This server requires the bearer token set in {TransportOptions.TokenVariable}.",
                });

                return;
            }

            await next(context);
        });
    }

    app.MapMcp(TransportOptions.Path);

    Announce(app.Services, options, transport.Endpoint);

    await app.RunAsync();
    return 0;
}

// Compared in constant time. The difference is unlikely to be measurable across a home network, but this
// is a shared secret compared on every request and there is no reason to hand out the timing.
static bool Presented(string header, string expected)
{
    const string Scheme = "Bearer ";

    if (!header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(header[Scheme.Length..].Trim()), Encoding.UTF8.GetBytes(expected));
}

// The first line in the log, and the one an operator reads to confirm which mode the server came up in:
// which installation, how many tools, and -- over HTTP -- the address to give the client.
static void Announce(IServiceProvider services, GleanvoltOptions options, Uri? endpoint)
{
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Gleanvolt.Mcp");

    if (endpoint is null)
    {
        logger.LogInformation(
            "Serving {Base} as {Count} tools over stdio; writes are {Writes}.",
            options.BaseAddress,
            ServerRegistration.ToolCount(options.AllowWrites),
            options.AllowWrites ? "ENABLED" : "disabled");

        return;
    }

    logger.LogInformation(
        "Serving {Base} as {Count} tools at {Endpoint}; writes are {Writes}.",
        options.BaseAddress,
        ServerRegistration.ToolCount(options.AllowWrites),
        endpoint,
        options.AllowWrites ? "ENABLED" : "disabled");
}
