namespace QCompact.Tests;

public class DiskpartCompactorTests
{
    [Fact]
    public void BuildScriptQuotesPathAndCompactsReadOnly()
    {
        var script = DiskpartCompactor.BuildScript(@"C:\Users\me\ext4.vhdx");

        Assert.Contains("select vdisk file=\"C:\\Users\\me\\ext4.vhdx\"", script);
        Assert.Contains("attach vdisk readonly", script);
        Assert.Contains("compact vdisk", script);
        Assert.Contains("detach vdisk", script);
        Assert.Contains("exit", script);
    }

    [Fact]
    public void BuildDetachScriptReleasesTheDisk()
    {
        var script = DiskpartCompactor.BuildDetachScript(@"C:\Users\me\ext4.vhdx");

        Assert.Contains("select vdisk file=\"C:\\Users\\me\\ext4.vhdx\"", script);
        Assert.Contains("detach vdisk", script);
        Assert.DoesNotContain("attach", script);
        Assert.Contains("exit", script);
    }

    [Theory]
    [InlineData(0, "DiskPart successfully compacted the virtual disk file.", "")]
    [InlineData(0, " 100 percent completed", "")]
    public void IsSuccessWhenNoFailureMarker(int exitCode, string output, string error)
    {
        Assert.True(DiskpartCompactor.IsSuccess(exitCode, output, error));
    }

    [Theory]
    [InlineData(1, "", "")]
    [InlineData(0, "Virtual Disk Service error:\r\nThe file is not found.", "")]
    [InlineData(0, "", "Access is denied.")]
    public void IsNotSuccessOnExitCodeOrFailureMarker(int exitCode, string output, string error)
    {
        Assert.False(DiskpartCompactor.IsSuccess(exitCode, output, error));
    }
}
