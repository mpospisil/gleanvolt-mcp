using System.Net.Http.Headers;
using Gleanvolt.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Gleanvolt.Mcp;

/// <summary>
/// Everything the server is, minus how it is spoken to. Both hosts in <c>Program</c> call this and
/// nothing else, so the tool surface a client sees over HTTP is the same surface it sees over stdio —
/// including which tools are missing. A transport that quietly registered a different set would be a
/// second place for the write switch to be wrong.
/// </summary>
internal static class ServerRegistration
{
    /// <summary>Registered always. Kept here so the startup log cannot drift from the truth.</summary>
    internal const int ReadTools = 9;

    /// <summary>Registered only with <see cref="GleanvoltOptions.AllowWrites"/>.</summary>
    internal const int WriteTools = 4;

    internal static int ToolCount(bool allowWrites) => allowWrites ? ReadTools + WriteTools : ReadTools;

    /// <summary>
    /// Returns the builder rather than swallowing it, so the caller chains its one transport onto the
    /// same registration. <c>AddMcpServer</c> called a second time to attach a transport would be a
    /// second server registration, not a continuation of this one.
    /// </summary>
    internal static IMcpServerBuilder AddGleanvoltMcpServer(
        this IServiceCollection services, GleanvoltOptions options, Transport transport)
    {
        services.AddSingleton(options);

        services.AddHttpClient<GleanvoltClient>(http =>
        {
            http.BaseAddress = options.BaseAddress;
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            // A forecast query on a Pi that is also polling Modbus is not instant, and a model that gives up
            // after 10 seconds will simply call again.
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        var mcp = services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new() { Name = "gleanvolt", Version = "0.1.0" };

                // What the client is told at initialize, and the only place a read-only server can explain
                // itself: the tools it would have refused with are not there to carry the explanation.
                server.ServerInstructions = ServerInstructions.For(options.AllowWrites, transport);
            })
            // The type-list overload rather than WithTools<T>(): these tool classes are static, and a static
            // type cannot be a type argument.
            .WithTools([typeof(ObservationTools), typeof(HistoryTools), typeof(PlanTools)]);

        // Registered rather than refused: a read-only server should not advertise a control surface it will
        // not use. The tool list itself is the switch.
        if (options.AllowWrites)
        {
            mcp.WithTools([typeof(ControlTools)]);
        }

        return mcp;
    }
}
