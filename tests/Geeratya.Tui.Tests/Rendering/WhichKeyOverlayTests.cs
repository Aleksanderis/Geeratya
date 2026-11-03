using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class WhichKeyOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenSpacePending_WhenBuilding_ThenEveryNextKeyShowsWithTheCatalogTitle()
    {
        var text = string.Join('\n', Build(Pending("Space"), 80, 24));

        foreach (var next in _keymap.NextKeys(InputMode.Normal, "Space").Where(next => next.Action is not null))
        {
            var title = ActionCatalog.FindTitle(next.Action!)!;

            Assert.Contains($"{next.Key,-10}{title[..Math.Min(20, title.Length)]}", text);
        }

        Assert.Contains("+issue (", text);
    }

    [Fact]
    public void GivenAPopupTooShortForBlankRows_WhenBuilding_ThenTheBlankRowsAreDropped()
    {
        var full = WhichKeyOverlay.Rows("Space", _keymap);
        var tight = WhichKeyOverlay.Rows("Space", _keymap, full.Count - 1);

        Assert.Contains(string.Empty, full);
        Assert.DoesNotContain(string.Empty, tight);
    }

    [Fact]
    public void GivenPendingKeys_WhenTheDelayHasNotPassed_ThenNoPopupShowsAndAfterItTheOneSharedRowsDo()
    {
        var since = DateTimeOffset.UnixEpoch;
        var pending = AppState.Initial with { PendingKeys = "Space", PendingSince = since };

        Assert.False(WhichKeyOverlay.Applies(pending));
        Assert.Same(pending, WhichKeyDelay.Opened(pending, since + TimeSpan.FromMilliseconds(249)));
        Assert.True(WhichKeyOverlay.Applies(WhichKeyDelay.Opened(pending, since + TimeSpan.FromMilliseconds(250))));
        Assert.DoesNotContain("┌", string.Join('\n', Build(pending, 80, 24)));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenANewChordStarts_ThenItStartsClosed()
    {
        var opened = Opened("Space");
        var later = DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5);
        var next = opened with { PendingKeys = "g", PendingSince = later };

        Assert.False(WhichKeyOverlay.Applies(next));
    }

    [Fact]
    public void GivenWhichKeyOff_WhenBuilding_ThenNoPopupShows()
    {
        var state = Pending("Space") with { Settings = new AppSettings(HintLevel.Full, false) };

        Assert.DoesNotContain("┌", string.Join('\n', Build(state, 80, 24)));
    }

    [Fact]
    public void GivenNothingPending_WhenBuilding_ThenNoPopupShows()
    {
        Assert.DoesNotContain("┌", string.Join('\n', Build(AppState.Initial, 80, 24)));
    }

    [Fact]
    public void GivenAnyTinySize_WhenBuildingWithKeysPending_ThenTheFrameStillFillsTheSize()
    {
        for (var width = 0; width <= 60; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                var lines = Build(Pending("Space"), width, height);

                Assert.Equal(width > 0 ? height : 0, lines.Count);
                Assert.All(lines, line => Assert.Equal(width, line.Length));
            }
        }
    }

    [Fact]
    public void GivenHintsOff_WhenRenderingTheStatusBar_ThenNoHintsShow()
    {
        var off = AppState.Initial with { Settings = new AppSettings(StatusHints: HintLevel.Off) };

        Assert.Contains("q quit", StatusBar.Render(AppState.Initial, 80, _keymap));
        Assert.DoesNotContain("quit", StatusBar.Render(off, 80, _keymap));
        Assert.Equal(80, StatusBar.Render(off, 80, _keymap).Length);
    }

    [Fact]
    public void GivenEveryHintList_WhenCheckingTheDefaultKeymap_ThenEachNamedActionIsBoundInItsMode()
    {
        IEnumerable<StatusHint> all =
            [.. StatusBar.Hints, .. StatusBar.ScreenHints, .. StatusBar.PickerHints, .. StatusBar.HelpHints, .. StatusBar.FormHints];

        foreach (var hint in all)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));

            if (hint.SecondActionId is not null)
            {
                Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.SecondActionId));
            }
        }
    }

    [Fact]
    public void GivenAnOpenPopup_WhenBuilding_ThenTheBoxSitsFlushRightAndTheLeftOfTheBodyStaysFree()
    {
        var lines = Build(Opened("Space"), 120, 36);
        var top = lines.Single(line => line.Contains("┌─ Space "));

        Assert.EndsWith("┐", top.TrimEnd());
        Assert.True(top.IndexOf('┌') > 120 - 52);
        Assert.Equal(120, top.Length);
    }

    /// <summary>A state with <paramref name="keys"/> pending and the popup open (the delay has passed).</summary>
    public static AppState Opened(string keys)
    {
        var since = DateTimeOffset.UnixEpoch;

        return AppState.Initial with { PendingKeys = keys, PendingSince = since, WhichKeyOpenFor = since };
    }

    private static AppState Pending(string keys)
    {
        return Opened(keys);
    }

    private static IReadOnlyList<string> Build(AppState state, int width, int height)
    {
        return FrameBuilder.Build(state, width, height, _keymap);
    }
}
