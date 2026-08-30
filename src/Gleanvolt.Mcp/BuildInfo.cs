using System.Reflection;

namespace Gleanvolt.Mcp;

/// <summary>
/// What this build actually is, read from the assembly's informational version.
///
/// <para>It exists because "which image is on the Pi?" had no answer from inside the running process.
/// The container's tag knows; the server did not, so anyone holding a log file could not tell which
/// build produced it. The version is logged once at startup and is what a client is told at
/// <c>initialize</c>.</para>
/// </summary>
internal static class BuildInfo
{
    /// <summary>
    /// The full informational version — <c>1.0.0+31bf347…</c> from CI, <c>0.0.0-dev</c> from a local
    /// build. Never null: an assembly with no attribute reports "unknown" rather than throwing.
    /// </summary>
    internal static string InformationalVersion { get; } =
        typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    /// <summary>The version without the commit, e.g. <c>1.0.0</c>.</summary>
    internal static string Version { get; } = Split(InformationalVersion).Version;

    /// <summary>
    /// The commit the build came from, or null when nothing stamped one — the normal case for a local
    /// build, and a reliable signal that this did not come from CI.
    /// </summary>
    internal static string? CommitSha { get; } = Split(InformationalVersion).Commit;

    /// <summary>The commit abbreviated to the 7 characters git and the image's <c>sha-</c> tags use.</summary>
    internal static string? ShortCommitSha => CommitSha is { Length: >= 7 } sha ? sha[..7] : CommitSha;

    /// <summary>Version and commit in one line, for the startup log.</summary>
    internal static string Describe() => ShortCommitSha is null ? Version : $"{Version} ({ShortCommitSha})";

    // The SDK composes InformationalVersion as "<version>+<SourceRevisionId>". Split on the *first* '+'
    // only: semver build metadata may contain further separators, and the commit is everything after
    // the first one.
    //
    // Internal rather than private so the suite can exercise it directly. Reading it back off this
    // assembly would only ever test the one version the test run happened to be built with.
    internal static (string Version, string? Commit) Split(string informational)
    {
        var plus = informational.IndexOf('+');

        return plus < 0
            ? (informational, null)
            : (informational[..plus], informational[(plus + 1)..] is { Length: > 0 } commit ? commit : null);
    }
}
