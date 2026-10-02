namespace QCompact.Tests;

public class SizeFormatterTests
{
    [Theory]
    [InlineData(512, "512.00 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(1073741824, "1.00 GB")]
    [InlineData(0, "0.00 B")]
    public void FormatsBytes(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.Format(bytes));
    }
}
