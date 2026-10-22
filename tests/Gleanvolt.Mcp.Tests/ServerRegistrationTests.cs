using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// What a client is actually offered, counted from the registration itself — and the count the startup
/// log reports, which is the first line an operator reads to tell whether the write switch reached the
/// process. The two have to agree, and both have to be the real tools.
///
/// <para>Counted from the container rather than from the type lists for a reason that shipped: a
/// <c>Type[]</c> handed to <c>WithTools</c> binds to the generic <c>WithTools&lt;T&gt;(T target)</c>,
/// which registers the tool methods of the array instance — none. The log, counting the lists, said 17
/// while the server answered every <c>tools/list</c> with "method not available".</para>
/// </summary>
public sealed class ServerRegistrationTests
{
    private static int Registered(bool allowWrites)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddGleanvoltMcpServer(
                new GleanvoltOptions(new Uri("http://127.0.0.1:9/"), "x", allowWrites),
                Transport.Stdio);

        using var provider = services.BuildServiceProvider();
        return provider.GetServices<McpServerTool>().Count();
    }

    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 17)]
    public void EveryToolIsRegisteredAndTheLogSaysHowMany(bool allowWrites, int expected)
    {
        Assert.Equal(expected, Registered(allowWrites));
        Assert.Equal(expected, ServerRegistration.ToolCount(allowWrites));
    }
}
