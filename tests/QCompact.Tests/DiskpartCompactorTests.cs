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
}
