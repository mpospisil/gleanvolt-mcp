namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// The version is what a bug report is filed against and the only thing a running process can say
/// about which image it came from — <c>:latest</c> names no version. These pin the parsing of the one
/// string the SDK gives us, including the local-build case, which is the one that must stay
/// distinguishable from a release.
/// </summary>
public sealed class BuildInfoTests
{
    /// <summary>The CI shape: a tag and the commit the SDK appended to it.</summary>
    [Fact]
    public void A_stamped_build_reports_its_version_and_commit()
    {
        var (version, commit) = BuildInfo.Split("1.0.0+31bf3479abc0000000000000000000000000000");

        Assert.Equal("1.0.0", version);
        Assert.Equal("31bf3479abc0000000000000000000000000000", commit);
    }

    /// <summary>
    /// The local-build shape, and the reason Directory.Build.props exists: no commit, and a version
    /// that says out loud it did not come from a tag.
    /// </summary>
    [Fact]
    public void An_unstamped_build_reports_no_commit()
    {
        var (version, commit) = BuildInfo.Split("0.0.0-dev");

        Assert.Equal("0.0.0-dev", version);
        Assert.Null(commit);
    }

    /// <summary>
    /// A trailing '+' with nothing after it is not a commit. It reads as "stamped, unknown revision",
    /// which is worse than saying nothing.
    /// </summary>
    [Fact]
    public void An_empty_revision_is_not_a_commit()
    {
        var (version, commit) = BuildInfo.Split("1.0.0+");

        Assert.Equal("1.0.0", version);
        Assert.Null(commit);
    }

    /// <summary>
    /// Split on the first '+' only. A prerelease carrying its own build metadata would otherwise lose
    /// half of the commit, and the shortened sha in the log would be right by luck.
    /// </summary>
    [Fact]
    public void Only_the_first_separator_splits()
    {
        var (version, commit) = BuildInfo.Split("1.0.0-rc.1+abc1234+extra");

        Assert.Equal("1.0.0-rc.1", version);
        Assert.Equal("abc1234+extra", commit);
    }

    /// <summary>
    /// What the log actually prints. Seven characters, matching git and the image's sha- tags, so the
    /// line in a log file can be pasted straight into `git show`.
    /// </summary>
    [Fact]
    public void This_assembly_describes_itself_without_throwing()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Describe()));
        Assert.Equal(BuildInfo.Version, BuildInfo.Split(BuildInfo.InformationalVersion).Version);

        if (BuildInfo.CommitSha is { Length: >= 7 })
        {
            Assert.Equal(7, BuildInfo.ShortCommitSha!.Length);
        }
    }
}
