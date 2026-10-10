using System.Text;

namespace QCompact.Tests;

public class SelfUpdateTests
{
    [Theory]
    [InlineData("1.1.2", "1.2.0", true)]
    [InlineData("1.2.0", "1.1.2", false)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.9.0", "1.10.0", true)]
    [InlineData("v1.1.2", "1.2.0", true)]
    [InlineData("1.2.0", "v1.2.0", false)]
    [InlineData("not-a-version", "1.2.0", false)]
    [InlineData("1.2.0", "not-a-version", false)]
    public void IsNewerComparesVersions(string current, string latest, bool expected)
    {
        Assert.Equal(expected, SelfUpdate.IsNewer(current, latest));
    }

    [Fact]
    public void MatchesChecksumVerifiesPublishedHash()
    {
        var exe = Path.Combine(Path.GetTempPath(), $"qcompact-test-{Guid.NewGuid():N}.exe");
        var sha = exe + ".sha256";

        try
        {
            File.WriteAllBytes(exe, Encoding.UTF8.GetBytes("qcompact"));
            var hash = SelfUpdate.Sha256OfFile(exe);

            File.WriteAllText(sha, $"{hash}  qcompact.exe{Environment.NewLine}");
            Assert.True(SelfUpdate.MatchesChecksum(exe, sha));

            File.WriteAllText(sha, $"{new string('0', 64)}  qcompact.exe{Environment.NewLine}");
            Assert.False(SelfUpdate.MatchesChecksum(exe, sha));

            File.Delete(sha);
            Assert.False(SelfUpdate.MatchesChecksum(exe, sha));
        }
        finally
        {
            File.Delete(exe);
            File.Delete(sha);
        }
    }

    [Fact]
    public void ReplaceBatchIsCrlfAndSwapsThenRelaunches()
    {
        // cmd.exe mis-parses LF-only batch files (goto/labels), so CRLF is required.
        var batch = SelfUpdate.BuildReplaceBatch(@"C:\temp\new.exe", @"C:\temp\new.exe.sha256", @"C:\apps\qcompact.exe")
            .ReplaceLineEndings("\r\n");

        Assert.DoesNotContain("\n", batch.Replace("\r\n", string.Empty));
        Assert.Contains("move /y \"%NEW%\" \"%TARGET%\"", batch);
        Assert.Contains("start \"\" \"%TARGET%\" --version", batch);
    }
}