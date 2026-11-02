using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class WhichKeyOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Fact]
    public void GivenSpacePending_WhenBuilding_ThenEveryNextKeyShowsWithTheCatalogTitle()
    {
        var text = string.Join('\n', Build(Pending("Space"), 80, 24));

        foreach (var next in _keymap.NextKeys(InputMode.Normal, "Space").Where(next => next.Action is not null))
        {
            Assert.Contains($"{next.Key} ", text);
            Assert.Contains(ActionCatalog.FindTitle(next.Action!)![..Math.Min(20, ActionCatalog.FindTitle(next.Action!)!.Length)], text);
        }

        Assert.Contains("+issue (1)", text);
    }

    [Fact]
    public void GivenWhichKeyOff_WhenBuilding_ThenNoPopupShows()
    {
        var state = Pending("Space") with { Settings = new AppSettings(WhichKey: false) };

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
        var off = AppState.Initial with { Settings = new AppSettings(StatusHints: false) };

        Assert.Contains("q quit", StatusBar.Render(AppState.Initial, 80, _keymap));
        Assert.DoesNotContain("quit", StatusBar.Render(off, 80, _keymap));
        Assert.Equal(80, StatusBar.Render(off, 80, _keymap).Length);
    }

    [Fact]
    public void GivenEveryHintList_WhenCheckingTheDefaultKeymap_ThenEachNamedActionIsBoundInItsMode()
    {
        IEnumerable<StatusHint> all =
            [.. StatusBar.Hints, .. StatusBar.ScreenHints, .. StatusBar.PickerHints, .. StatusBar.FormHints];

        foreach (var hint in all)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));

            if (hint.SecondActionId is not null)
            {
                Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.SecondActionId));
            }
        }
    }

    private static AppState Pending(string keys)
    {
        return AppState.Initial with { PendingKeys = keys };
    }

    private static IReadOnlyList<string> Build(AppState state, int width, int height)
    {
        return FrameBuilder.Build(state, width, height, _helpLines, _keymap);
    }
}
