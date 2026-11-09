using DiskCleaner.Core.Formatting;

namespace DiskCleaner.Tests;

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(524_288_000, "500,0 MB")]
    [InlineData(1_610_612_736, "1,5 GB")]
    public void Format_UsesPtBrAndBase1024(long bytes, string expected) =>
        Assert.Equal(expected, SizeFormatter.Format(bytes));
}
