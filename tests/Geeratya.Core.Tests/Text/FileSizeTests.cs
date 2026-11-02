using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class FileSizeTests
{
    [Theory]
    [InlineData(-5, "0 B")]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KiB")]
    [InlineData(1536, "1.5 KiB")]
    [InlineData(1_048_575, "1.0 MiB")]
    [InlineData(1_048_576, "1.0 MiB")]
    [InlineData(3_500_000, "3.3 MiB")]
    [InlineData(1_073_741_824, "1.0 GiB")]
    [InlineData(1_099_511_627_776, "1.0 TiB")]
    [InlineData(long.MaxValue, "8388608.0 TiB")]
    public void GivenByteCount_WhenFormatting_ThenBinaryUnitsWithOneDecimalAreUsed(long bytes, string expected)
    {
        Assert.Equal(expected, FileSize.Format(bytes));
    }
}
