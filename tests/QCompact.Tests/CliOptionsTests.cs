namespace QCompact.Tests;

public class CliOptionsTests
{
    [Fact]
    public void DefaultsToNoFlagsAndNoDistros()
    {
        var options = CliOptions.Parse(Array.Empty<string>());

        Assert.Empty(options.Distros);
        Assert.False(options.DryRun);
        Assert.False(options.Help);
        Assert.False(options.Version);
        Assert.False(options.SelfUpdate);
    }

    [Fact]
    public void ParsesSelfUpdate()
    {
        var options = CliOptions.Parse(new[] { "self-update" });

        Assert.True(options.SelfUpdate);
    }

    [Fact]
    public void ParsesFlags()
    {
        var options = CliOptions.Parse(new[] { "--dry-run", "--yes", "--json", "--verbose", "--elevate", "--keep-log" });

        Assert.True(options.DryRun);
        Assert.True(options.Yes);
        Assert.True(options.Json);
        Assert.True(options.Verbose);
        Assert.True(options.Elevate);
        Assert.True(options.KeepLog);
    }

    [Fact]
    public void ElevatesByDefaultAndNoElevateDisablesIt()
    {
        Assert.True(CliOptions.Parse(Array.Empty<string>()).Elevate);

        var options = CliOptions.Parse(new[] { "--no-elevate" });
        Assert.False(options.Elevate);
    }

    [Fact]
    public void CollectsRepeatedDistroValues()
    {
        var options = CliOptions.Parse(new[] { "-d", "Ubuntu", "--distro", "Debian", "--distro=Fedora" });

        Assert.Equal(new[] { "Ubuntu", "Debian", "Fedora" }, options.Distros);
    }

    [Fact]
    public void ThrowsOnUnknownOption()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "--nope" }));
    }

    [Fact]
    public void ThrowsWhenDistroValueMissing()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "--distro" }));
    }
}
