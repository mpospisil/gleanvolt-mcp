using System.Net.Http.Headers;
using Gleanvolt.Mcp;
using Gleanvolt.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

GleanvoltOptions options;

try
{
    options = GleanvoltOptions.FromEnvironment();
}
catch (InvalidOperationException exception)
{
    // stderr, and a non-zero exit: an MCP client reads stdout as protocol frames, so a message written
    // there would be a parse error rather than an explanation.
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

// Everything the logger emits goes to stderr. The default console provider writes to stdout, which is
// the protocol stream -- one stray informational line and the client drops the connection.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(options);

builder.Services.AddHttpClient<GleanvoltClient>(http =>
{
    http.BaseAddress = options.BaseAddress;
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

    // A forecast query on a Pi that is also polling Modbus is not instant, and a model that gives up
    // after 10 seconds will simply call again.
    http.Timeout = TimeSpan.FromSeconds(30);
});

var mcp = builder.Services
    .AddMcpServer(server => server.ServerInfo = new() { Name = "gleanvolt", Version = "0.1.0" })
    .WithStdioServerTransport()
    // The type-list overload rather than WithTools<T>(): these tool classes are static, and a static
    // type cannot be a type argument.
    .WithTools([typeof(ObservationTools), typeof(HistoryTools), typeof(PlanTools)]);

// Registered rather than refused: a read-only server should not advertise a control surface it will
// not use. The tool list itself is the switch.
if (options.AllowWrites)
{
    mcp.WithTools([typeof(ControlTools)]);
}

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Gleanvolt.Mcp")
    .LogInformation(
        "Serving {Base} as {Count} tools; writes are {Writes}.",
        options.BaseAddress,
        options.AllowWrites ? 13 : 9,
        options.AllowWrites ? "ENABLED" : "disabled");

await host.RunAsync();
return 0;
