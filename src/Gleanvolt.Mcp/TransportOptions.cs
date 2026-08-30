namespace Gleanvolt.Mcp;

/// <summary>How a client reaches this server. Not how this server reaches the installation — that is
/// <see cref="GleanvoltOptions"/>, and it is the same either way.</summary>
internal enum Transport
{
    /// <summary>One server process per client, launched by the client, framed over stdin and stdout.</summary>
    Stdio,

    /// <summary>One long-running server process, reached over the network by any number of clients.</summary>
    Http,
}

/// <summary>
/// Which host <c>Program</c> builds, and what it binds.
///
/// <para>The default is <see cref="Transport.Stdio"/>, so a registration written before this existed
/// still launches the server it launched before. HTTP is what Home Assistant's <c>mcp</c> integration
/// needs: that integration is a client with no way to spawn anything, so there has to be something
/// already listening for it to point at.</para>
/// </summary>
internal sealed record TransportOptions(Transport Kind, Uri BindAddress, string? Token)
{
    internal const string Variable = "GLEANVOLT_MCP_TRANSPORT";

    internal const string BindVariable = "GLEANVOLT_MCP_HTTP_URL";

    internal const string TokenVariable = "GLEANVOLT_MCP_HTTP_TOKEN";

    /// <summary>
    /// Where the Streamable HTTP endpoint is mounted. Fixed rather than configurable: it is the
    /// convention every client's documentation uses, and the whole address a person has to type into
    /// Home Assistant is short enough already without a path they also have to remember.
    /// </summary>
    internal const string Path = "/mcp";

    /// <summary>
    /// All interfaces, because the point of HTTP mode is to be reached from another machine or another
    /// container — a loopback default would bind successfully and refuse every client. Port 8091 sits
    /// next to the installation's own 8090.
    /// </summary>
    internal const string DefaultBindAddress = "http://0.0.0.0:8091";

    /// <summary>The full address a client is pointed at, for the startup log and nothing else.</summary>
    internal Uri Endpoint => new(BindAddress, Path);

    internal static TransportOptions FromEnvironment()
    {
        var kind = ParseKind(Environment.GetEnvironmentVariable(Variable));

        var bind = Environment.GetEnvironmentVariable(BindVariable);
        bind = string.IsNullOrWhiteSpace(bind) ? DefaultBindAddress : bind.Trim();

        if (!Uri.TryCreate(bind, UriKind.Absolute, out var bindAddress))
        {
            throw new InvalidOperationException(
                $"{BindVariable} must be an absolute address to bind, e.g. {DefaultBindAddress}. "
                + $"Got: {bind}");
        }

        var token = Environment.GetEnvironmentVariable(TokenVariable);

        return new TransportOptions(
            kind, bindAddress, string.IsNullOrWhiteSpace(token) ? null : token.Trim());
    }

    /// <summary>
    /// An unrecognised value is an error rather than a fallback, which is the opposite of how
    /// <see cref="GleanvoltOptions.WritesVariable"/> is read — and for the opposite reason. There, a
    /// typo has a safe direction to fall in: the hardware is left alone. Here it does not. A
    /// misspelled <c>htpp</c> that quietly started a stdio server would sit waiting on a stdin nobody
    /// is writing to, look like a hang, and tell nobody why.
    /// </summary>
    private static Transport ParseKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Transport.Stdio;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "stdio" => Transport.Stdio,
            "http" => Transport.Http,
            _ => throw new InvalidOperationException(
                $"{Variable} must be 'stdio' or 'http'. Got: {value}"),
        };
    }
}
