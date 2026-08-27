namespace Gleanvolt.Mcp;

/// <summary>
/// Everything this server needs to reach one installation, read from the environment because that is
/// how an MCP client launches a stdio server — there is no config file to put a key in, and no user
/// sitting in front of it to type one.
/// </summary>
internal sealed record GleanvoltOptions(Uri BaseAddress, string ApiKey, bool AllowWrites)
{
    internal const string UrlVariable = "GLEANVOLT_URL";

    internal const string KeyVariable = "GLEANVOLT_API_KEY";

    internal const string WritesVariable = "GLEANVOLT_MCP_ALLOW_WRITES";

    /// <summary>
    /// Fails fast and loudly on stderr rather than starting a server whose every tool would answer
    /// with the same connection error. An MCP client shows a server that exits at launch as broken,
    /// which is the honest report.
    /// </summary>
    internal static GleanvoltOptions FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        var key = Environment.GetEnvironmentVariable(KeyVariable);

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                $"{UrlVariable} must be the base address of a Gleanvolt installation, "
                + "e.g. http://gleanvolt.local:8090");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"{KeyVariable} must be one of the installation's Api:Keys. Generate one with "
                + "Api__Keys__claude-mcp=$(openssl rand -hex 32) and enable Api__Enabled=true.");
        }

        // Writes are opt-in for the same reason the API itself is off by default: four of these tools
        // move real hardware, and an operator switches that on knowingly. Anything other than a
        // deliberate "true" leaves this server read-only.
        var writes = string.Equals(
            Environment.GetEnvironmentVariable(WritesVariable), "true", StringComparison.OrdinalIgnoreCase);

        return new GleanvoltOptions(baseAddress, key, writes);
    }
}
