using Geeratya.Core.Boards;
using Geeratya.Core.Settings;

namespace Geeratya.Core.Tests.Settings;

[Trait("Os", "Independent")]
public class DoneCutoffSettingTests
{
    private static SettingDefinition Setting => SettingsRegistry.Find("board.done-cutoff")!;

    [Theory]
    [InlineData("7", 7)]
    [InlineData("3650", 3650)]
    [InlineData("all", 0)]
    [InlineData("ALL", 0)]
    public void GivenAValidValue_WhenApplied_ThenTheDaysAreSet(string text, int days)
    {
        var applied = Setting.Apply(AppSettings.Default, text);

        Assert.Equal(days, applied?.DoneCutoffDays);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("3651")]
    [InlineData("1.5")]
    [InlineData("week")]
    [InlineData("")]
    public void GivenAnInvalidValue_WhenApplied_ThenItIsRefused(string text)
    {
        Assert.Null(Setting.Apply(AppSettings.Default, text));
    }

    [Fact]
    public void GivenDefaults_WhenRead_ThenFourteenDaysAndAllReadsAsAll()
    {
        Assert.Equal(DoneCutoff.DefaultDays, AppSettings.Default.DoneCutoffDays);
        Assert.Equal("14", Setting.Read(AppSettings.Default));
        Assert.Equal("all", Setting.Read(new AppSettings(DoneCutoffDays: 0)));
    }
}
