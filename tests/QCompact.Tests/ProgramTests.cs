namespace QCompact.Tests;

public class ProgramTests
{
    [Fact]
    public void HelpReturnsZero()
    {
        Assert.Equal(0, Program.Main(new[] { "--help" }));
    }

    [Fact]
    public void VersionReturnsZero()
    {
        Assert.Equal(0, Program.Main(new[] { "--version" }));
    }

    [Fact]
    public void UnknownOptionReturnsTwo()
    {
        Assert.Equal(2, Program.Main(new[] { "--bogus" }));
    }

    [Fact]
    public void DryRunReturnsZero()
    {
        Assert.Equal(0, Program.Main(new[] { "--dry-run" }));
    }
}
