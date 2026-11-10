using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Updates;

namespace Geeratya.Core.Tests.Updates;

public class UpdateReducerTests
{
    private static readonly UpdateNotice _notice = new("1.2.3", "run `geeratya update`");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenTheSetting_WhenTheShellAsksForTheCheck_ThenTheEffectCarriesIt(bool enabled)
    {
        var state = AppState.Initial with { Settings = AppSettings.Default with { UpdateCheck = enabled } };

        var result = NewReducer().Reduce(state, new UpdateCheckRequested());

        Assert.Equal(enabled, Assert.IsType<CheckForUpdateEffect>(Assert.Single(result.Effects)).Enabled);
    }

    [Fact]
    public void GivenANewerRelease_WhenTheCheckAnswers_ThenTheNoticeStaysAndTheSentenceIsPostedOnce()
    {
        var reducer = NewReducer();

        var first = reducer.Reduce(AppState.Initial, new UpdateAvailable(_notice));
        var second = reducer.Reduce(first.State, new UpdateAvailable(_notice));

        Assert.Equal(_notice, first.State.Update);
        Assert.Equal("Geeratya 1.2.3 is available: run `geeratya update`", first.State.Status?.Text);
        Assert.Single(second.State.StatusHistory, message => message.Text.Contains("1.2.3", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheDefaults_WhenReadingTheSetting_ThenTheCheckIsOnByDefault()
    {
        Assert.True(AppSettings.Default.UpdateCheck);
        Assert.Equal("on", SettingsRegistry.Find("update.check")!.Default);
    }

    private static AppReducer NewReducer()
    {
        return new AppReducer(DefaultKeymap.Load());
    }
}
