using System.Net.Http.Headers;
using System.Reflection;
using Gleanvolt.Mcp.Tools;
using ModelContextProtocol.Server;
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
    /// <summary>The tool classes registered always.</summary>
    private static readonly Type[] ReadToolTypes =
        [typeof(ObservationTools), typeof(HistoryTools), typeof(PlanTools), typeof(TariffTools)];

    /// <summary>The tool classes registered only with <see cref="GleanvoltOptions.AllowWrites"/>.</summary>
    private static readonly Type[] WriteToolTypes = [typeof(ControlTools), typeof(TariffControlTools)];

    /// <summary>
    /// The tools registered always, counted from the same lists the registration uses, so the startup
    /// log cannot drift from the truth. It did when this was a constant: two tools were added and the
    /// log went on saying the old number.
    /// </summary>
    internal static int ReadTools => Count(ReadToolTypes);

    /// <summary>Registered only with <see cref="GleanvoltOptions.AllowWrites"/>.</summary>
    internal static int WriteTools => Count(WriteToolTypes);

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
                // The version a client is shown at initialize is the build's own, not a literal kept in
                // step by hand. A hardcoded one goes stale silently: nothing fails when it disagrees
                // with the image tag, it just misleads whoever is reading it.
                server.ServerInfo = new() { Name = "gleanvolt", Version = BuildInfo.Version };

                // What the client is told at initialize, and the only place a read-only server can explain
                // itself: the tools it would have refused with are not there to carry the explanation.
                server.ServerInstructions = ServerInstructions.For(options.AllowWrites, transport);
            })
            // The type-list overload rather than WithTools<T>(): these tool classes are static, and a static
            // type cannot be a type argument.
            .WithTools(ReadToolTypes);

        // Registered rather than refused: a read-only server should not advertise a control surface it will
        // not use. The tool list itself is the switch.
        if (options.AllowWrites)
        {
            mcp.WithTools(WriteToolTypes);
        }

        return mcp;
    }

    private static int Count(IEnumerable<Type> types) =>
        types.Sum(type => type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Count(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null));
}
