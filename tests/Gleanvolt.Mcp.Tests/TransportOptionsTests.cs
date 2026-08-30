namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// The transport is read once, at launch, and everything after it is decided by what it said. These
/// pin the two things a person actually gets wrong: leaving the variable off entirely, which must keep
/// launching the stdio server that every existing registration expects, and misspelling it, which must
/// not silently become one.
///
/// <para>Each test sets the variables it cares about and clears them again, so the process environment
/// is the same on the way out as on the way in — xUnit runs a collection on one process.</para>
/// </summary>
[Collection(nameof(TransportOptionsTests))]
[CollectionDefinition(nameof(TransportOptionsTests), DisableParallelization = true)]
public sealed class TransportOptionsTests : IDisposable
{
    public void Dispose()
    {
        foreach (var variable in new[]
                 {
                     TransportOptions.Variable,
                     TransportOptions.BindVariable,
                     TransportOptions.TokenVariable,
                 })
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>
    /// The compatibility guarantee, and the reason this is the first test in the file. Every
    /// `claude mcp add` registration written before HTTP existed passes no transport at all.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unset_transport_is_stdio(string? value)
    {
        Environment.SetEnvironmentVariable(TransportOptions.Variable, value);

        Assert.Equal(Transport.Stdio, TransportOptions.FromEnvironment().Kind);
    }

    [Theory]
    [InlineData("stdio", false)]
    [InlineData("http", true)]
    [InlineData("HTTP", true)]
    [InlineData("  Http  ", true)]
    public void A_named_transport_is_read_case_and_space_insensitively(string value, bool http)
    {
        Environment.SetEnvironmentVariable(TransportOptions.Variable, value);

        // The expectation arrives as a bool because xUnit's inline data is part of a public signature
        // and Transport, like everything else in this assembly, is internal.
        Assert.Equal(http ? Transport.Http : Transport.Stdio, TransportOptions.FromEnvironment().Kind);
    }

    /// <summary>
    /// Deliberately unlike <see cref="GleanvoltOptions.WritesVariable"/>, which treats anything it does
    /// not recognise as "no". A typo there leaves the hardware alone, which is the safe direction to
    /// fall in. A typo here has no safe direction: a stdio server started by mistake would sit reading
    /// a stdin nobody is writing to, and look to its operator exactly like a hang.
    /// </summary>
    [Theory]
    [InlineData("htpp")]
    [InlineData("sse")]
    [InlineData("true")]
    public void An_unrecognised_transport_refuses_to_launch(string value)
    {
        Environment.SetEnvironmentVariable(TransportOptions.Variable, value);

        var error = Assert.Throws<InvalidOperationException>(TransportOptions.FromEnvironment);

        // The variable and the value it was given: enough to fix it from the log line alone.
        Assert.Contains(TransportOptions.Variable, error.Message, StringComparison.Ordinal);
        Assert.Contains(value, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_bind_address_defaults_to_every_interface()
    {
        var transport = TransportOptions.FromEnvironment();

        // Not loopback. A server bound to 127.0.0.1 inside a container binds successfully and then
        // refuses every client on the compose network, which is the whole of HTTP mode's audience.
        Assert.Equal(new Uri(TransportOptions.DefaultBindAddress), transport.BindAddress);
    }

    [Fact]
    public void The_endpoint_is_the_bind_address_plus_the_mounted_path()
    {
        Environment.SetEnvironmentVariable(TransportOptions.BindVariable, "http://0.0.0.0:9000");

        // What the log prints and what a person types into Home Assistant's config flow.
        Assert.Equal(new Uri("http://0.0.0.0:9000/mcp"), TransportOptions.FromEnvironment().Endpoint);
    }

    [Fact]
    public void An_unusable_bind_address_refuses_to_launch()
    {
        Environment.SetEnvironmentVariable(TransportOptions.BindVariable, "8091");

        var error = Assert.Throws<InvalidOperationException>(TransportOptions.FromEnvironment);

        Assert.Contains(TransportOptions.BindVariable, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No token is the documented configuration for Home Assistant, whose config flow asks for a URL
    /// and has nowhere to put a bearer token. Blank has to mean the same as absent, or a compose file
    /// that passes an unset variable through would gate the endpoint on the empty string.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_unset_token_leaves_the_endpoint_ungated(string? value)
    {
        Environment.SetEnvironmentVariable(TransportOptions.TokenVariable, value);

        Assert.Null(TransportOptions.FromEnvironment().Token);
    }

    [Fact]
    public void A_token_is_taken_as_given_once_trimmed()
    {
        Environment.SetEnvironmentVariable(TransportOptions.TokenVariable, "  s3cret\t");

        Assert.Equal("s3cret", TransportOptions.FromEnvironment().Token);
    }
}
