namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// The count the startup log reports is the count registered — the first line an operator reads to
/// tell whether the write switch reached the process.
/// </summary>
public sealed class ServerRegistrationTests
{
    [Fact]
    public void TheLoggedCountsAreTheRegisteredTools()
    {
        Assert.Equal(11, ServerRegistration.ToolCount(allowWrites: false));
        Assert.Equal(17, ServerRegistration.ToolCount(allowWrites: true));
    }
}
