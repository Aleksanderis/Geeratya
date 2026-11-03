using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusBarTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    private static readonly string[] _columnsHints =
        ["h/l column", "j/k card", "Enter open", "Space l list", "Space o browser"];

    [Fact]
    public void GivenVisualModeOrMarks_WhenRenderingTheStatusLine_ThenTheBadgeAndTheCountShow()
    {
        var tab = TabState.Opened(
            new TabId(1),
            new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A"),
            1,
            1,
            10);
        var visual = ActiveOf(tab with { VisualAnchor = "A-1" });
        var marked = ActiveOf(tab with { Marks = ["A-1", "A-2", "A-3"] });

        Assert.Contains("VISUAL", StatusBar.Render(visual, 80, _keymap));
        Assert.Contains("3 selected", StatusBar.Render(marked, 80, _keymap));
    }

    [Fact]
    public void GivenEveryHint_WhenCheckingTheDefaultKeymap_ThenItBindsTheNamedActionInTheHintMode()
    {
        foreach (var hint in StatusBar.Hints)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));
            Assert.NotNull(ActionCatalog.FindTitle(hint.ActionId));
        }
    }

    [Fact]
    public void GivenEveryDefaultHint_WhenCheckingTheLevel_ThenNoneIsAdvanced()
    {
        var all = StatusBar.Hints
            .Concat(StatusBar.ScreenHints)
            .Concat(StatusBar.PickerHints)
            .Concat(StatusBar.HelpHints)
            .Concat(StatusBar.EditorHints)
            .Concat(StatusBar.FormHints);

        Assert.All(all, hint => Assert.True(StatusBar.IsShownAt(hint, CommandLevel.Everyday), hint.ActionId));
    }

    [Fact]
    public void GivenHintsOfAdvancedActions_WhenCheckingTheLevel_ThenOnlyAllAdvancedOnesAreLeftOutAtEveryday()
    {
        var advanced = new StatusHint(InputMode.Normal, ActionIds.IssueDelete, "delete");
        var pair = new StatusHint(InputMode.Normal, ActionIds.RankTop, "rank", HintScope.Global, ActionIds.IssueDelete);
        var mixed = new StatusHint(
            InputMode.Normal,
            ActionIds.IssueDelete,
            "x",
            HintScope.Global,
            ActionIds.NavigateUp);

        Assert.False(StatusBar.IsShownAt(advanced, CommandLevel.Everyday));
        Assert.False(StatusBar.IsShownAt(pair, CommandLevel.Everyday));
        Assert.True(StatusBar.IsShownAt(mixed, CommandLevel.Everyday));
        Assert.True(StatusBar.IsShownAt(advanced, CommandLevel.Advanced));
    }

    [Fact]
    public void GivenNormalMode_WhenRendering_ThenBadgeStartsAtColumnZeroAndHintsAreRightAligned()
    {
        var line = StatusBar.Render(AppState.Initial, 60, _keymap);

        Assert.StartsWith(" NORMAL", line);
        Assert.EndsWith("q quit", line);
        Assert.Equal(60, line.Length);
    }

    [Fact]
    public void GivenATypedCount_WhenRendering_ThenTheCountAndPendingKeysAreShown()
    {
        var counted = AppState.Initial with { Count = 12 };
        var pending = counted with { PendingKeys = "Space k" };

        Assert.Contains("NORMAL 12", StatusBar.Render(counted, 60, _keymap));
        Assert.Contains("NORMAL 12 Space k", StatusBar.Render(pending, 60, _keymap));
    }

    [Fact]
    public void GivenAPickedUpIssue_WhenRendering_ThenTheStatusLineSaysSoUntilItIsDropped()
    {
        var tab = TabState.Opened(
            new TabId(1),
            new ViewDefinition(ViewSourceKind.Backlog, "4", ViewLayout.List, "B"),
            1,
            1,
            10) with { RankPickup = "A-7" };
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };

        Assert.Contains("Picked up A-7", StatusBar.Render(state, 120, _keymap));
    }

    [Fact]
    public void GivenCommandMode_WhenRendering_ThenTypedTextIsShownAfterTheColon()
    {
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = "quit" };

        Assert.Contains("COMMAND :quit", StatusBar.Render(state, 60, _keymap));
    }

    [Fact]
    public void GivenNarrowWidth_WhenRendering_ThenHintsAreDroppedBeforeTheStatusText()
    {
        var state = AppState.Initial with { Message = "Unknown command: x" };

        var line = StatusBar.Render(state, 30, _keymap);

        Assert.DoesNotContain("quit", line);
        Assert.Contains("Unknown command: x", line);
        Assert.Equal(30, line.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void GivenTinyWidth_WhenRendering_ThenLineNeverExceedsTheWidth(int width)
    {
        foreach (var mode in Enum.GetValues<InputMode>())
        {
            var line = StatusBar.Render(FrameOptions.SampleState(mode), width, _keymap);

            Assert.True(line.Length <= width);
        }
    }

    [Fact]
    public void GivenNarrowWidth_WhenConfirming_ThenTheQuestionIsCutAndTheAnswerKeysStay()
    {
        var state = FrameOptions.SampleState(InputMode.Confirm);

        var line = StatusBar.Render(state, 30, _keymap);

        Assert.EndsWith("(y/n)", line.TrimEnd());
        Assert.Equal(30, line.Length);
    }

    [Fact]
    public void GivenARemappedQuitKey_WhenRenderingHints_ThenTheHintShowsTheNewKey()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "Ctrl-q", ActionIds.Quit)]);

        Assert.Equal(["Ctrl-q quit"], StatusBar.HintsFor(InputMode.Normal, keymap));
    }

    [Fact]
    public void GivenAHintActionUnbound_WhenRenderingHints_ThenItIsLeftOut()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "q", ActionIds.Quit)]);

        Assert.Equal(["q quit"], StatusBar.HintsFor(InputMode.Normal, keymap));
    }

    [Fact]
    public void GivenARemappedConfirmKey_WhenConfirming_ThenTheSuffixShowsTheNewKeys()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Confirm, "Y", ActionIds.ConfirmYes),
            new KeyBinding(InputMode.Confirm, "n", ActionIds.ConfirmNo),
        ]);

        var line = StatusBar.Render(FrameOptions.SampleState(InputMode.Confirm), 80, keymap);

        Assert.Contains("(Y/n)", line);
    }

    [Fact]
    public void GivenDefaultKeymap_WhenRenderingHelpHints_ThenCloseShowsEscNotQ()
    {
        Assert.Contains("Esc close", StatusBar.HintsFor(InputMode.Help, _keymap));
    }

    [Fact]
    public void GivenHelpPages_WhenRenderingTheStatusLine_ThenTheHintsNameTheSectionScrollAndFilterKeys()
    {
        var line = StatusBar.Render(AppState.Initial with { Mode = InputMode.Help }, 80, _keymap);

        Assert.Contains("j section", line);
        Assert.Contains("J scroll", line);
        Assert.Contains("/ filter", line);
    }

    [Fact]
    public void GivenTheMessagesOverlay_WhenRenderingTheStatusLine_ThenThePageAndFilterHintsAreLeftOut()
    {
        var state = StatusMessages.OpenHistory(AppState.Initial);

        var line = StatusBar.Render(state, 80, _keymap);

        Assert.DoesNotContain("page", line);
        Assert.DoesNotContain("filter", line);
    }

    [Fact]
    public void GivenTokenStep_WhenRendering_ThenTokenIsMaskedAndLengthShown()
    {
        var form = ProfileForm.Start with { Field = ProfileFormField.Token, Token = new SecretText("abc") };
        var state = AppState.Initial with { Mode = InputMode.Insert, Form = form };

        var line = StatusBar.Render(state, 80, _keymap);

        Assert.Contains("INSERT [4/4] API token (hidden): ***_", line);
        Assert.DoesNotContain("abc", line);
        Assert.Contains("Enter save", line);
        Assert.DoesNotContain("Enter next", line);
        Assert.Contains("Esc cancel", line);
    }

    [Fact]
    public void GivenFormError_WhenRendering_ThenErrorFollowsThePrompt()
    {
        var form = ProfileForm.Start with { Error = "The name cannot be empty." };
        var state = AppState.Initial with { Mode = InputMode.Insert, Form = form };

        Assert.Contains("[1/4] ! The name cannot be empty.", StatusBar.Render(state, 100, _keymap));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(80)]
    public void GivenLongToken_WhenRenderingNarrow_ThenTheCursorAndTheEndOfTheMaskStayVisible(int width)
    {
        var form = ProfileForm.Start with { Field = ProfileFormField.Token, Token = new SecretText(new string('t', 150)) };
        var state = AppState.Initial with { Mode = InputMode.Insert, Form = form };

        var line = StatusBar.Render(state, width, _keymap);

        Assert.Equal(width, TextWidth.Of(line));
        Assert.Contains("*_", line.TrimEnd());
    }

    [Fact]
    public void GivenLongSiteUrl_WhenRenderingNarrow_ThenTheTailOfTheTextIsShown()
    {
        var form = ProfileForm.Start with { Field = ProfileFormField.SiteUrl, SiteUrl = "https://example.atlassian.net" };
        var state = AppState.Initial with { Mode = InputMode.Insert, Form = form };

        var line = StatusBar.Render(state, 40, _keymap);

        Assert.Contains("atlassian.net_", line);
        Assert.DoesNotContain("https://", line);
    }

    [Fact]
    public void GivenEveryScreenHint_WhenCheckingTheDefaultKeymap_ThenItBindsBothNamedActions()
    {
        foreach (var hint in StatusBar.ScreenHints)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));
            Assert.NotNull(ActionCatalog.FindTitle(hint.ActionId));

            if (hint.SecondActionId is not null)
            {
                Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.SecondActionId));
            }
        }
    }

    [Fact]
    public void GivenColumnsLayout_WhenRendering120Wide_ThenTheColumnsHintsShowBeforeTheGlobalOnes()
    {
        var line = StatusBar.Render(With(ViewLayout.Columns), 120, _keymap);

        Assert.Contains("h/l column  j/k card  Enter open  Space l list  Space o browser", line);
        Assert.EndsWith("? help  : commands  q quit", line);
        Assert.Equal(120, line.Length);
    }

    [Fact]
    public void GivenColumnsLayoutAndALongMessage_WhenRendering40Wide_ThenAllHintsAreDroppedBeforeTheText()
    {
        var line = StatusBar.Render(With(ViewLayout.Columns, "Switched to a long profile"), 40, _keymap);

        Assert.DoesNotContain("column", line);
        Assert.DoesNotContain("card", line);
        Assert.DoesNotContain("browser", line);
        Assert.Contains("Switched to a long profile", line);
        Assert.Equal(40, line.Length);
    }

    [Fact]
    public void GivenMinimalHintsAndColumnsLayout_WhenRendering_ThenOnlyTheThreeGlobalHintsShow()
    {
        var full = With(ViewLayout.Columns);
        var minimal = full with { Settings = new AppSettings(StatusHints: HintLevel.Minimal) };

        Assert.Contains("h/l column", StatusBar.Render(full, 120, _keymap));

        var line = StatusBar.Render(minimal, 120, _keymap);

        Assert.DoesNotContain("column", line);
        Assert.DoesNotContain("browser", line);
        Assert.EndsWith("? help  : commands  q quit", line);
        Assert.Equal(120, line.Length);
    }

    [Fact]
    public void GivenColumnsLayout_WhenRendering50WideWithoutAMessage_ThenScreenHintsDropAndGlobalOnesStay()
    {
        var line = StatusBar.Render(With(ViewLayout.Columns), 50, _keymap);

        Assert.DoesNotContain("column", line);
        Assert.DoesNotContain("card", line);
        Assert.EndsWith("? help  : commands  q quit", line);
    }

    [Fact]
    public void GivenColumnsLayout_WhenWidthShrinks_ThenScreenHintsDropFromTheEndOneByOne()
    {
        var counts = new List<int>();

        for (var width = 120; width >= 60; width -= 4)
        {
            var line = StatusBar.Render(With(ViewLayout.Columns), width, _keymap);

            counts.Add(_columnsHints.Count(line.Contains));
            Assert.Contains("q quit", line);
        }

        Assert.Equal(counts.OrderByDescending(count => count), counts);
        Assert.Equal(5, counts[0]);
        Assert.Contains(counts, count => count is > 0 and < 5);
    }

    [Fact]
    public void GivenListLayoutOnAJqlSource_WhenRendering_ThenNoLayoutSwitchIsOffered()
    {
        var line = StatusBar.Render(With(ViewLayout.List, source: ViewSourceKind.Jql), 120, _keymap);

        Assert.Contains("j/k move  Enter open  Space o browser", line);
        Assert.DoesNotContain("columns", line);
    }

    [Fact]
    public void GivenListLayoutOnABoard_WhenRendering_ThenTheColumnsSwitchIsOffered()
    {
        var line = StatusBar.Render(With(ViewLayout.List, source: ViewSourceKind.Board), 120, _keymap);

        Assert.Contains("Space l columns", line);
    }

    [Fact]
    public void GivenAnIssueOpen_WhenRendering_ThenTheDetailHintsReplaceTheViewOnes()
    {
        var state = With(ViewLayout.Columns, detail: true);

        var line = StatusBar.Render(state, 120, _keymap);

        Assert.Contains("j/k scroll  Enter open  Esc back  Space o browser  Space p parent", line);
        Assert.DoesNotContain("card", line);
    }

    [Fact]
    public void GivenARemappedBrowserKey_WhenRendering_ThenTheHintShowsTheNewKey()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "Space b", ActionIds.IssueOpenInBrowser)]);

        Assert.Equal(["Space b browser"], StatusBar.ScreenHintsFor(HintScope.List, keymap));
    }

    [Fact]
    public void GivenNoTab_WhenRendering_ThenOnlyTheGlobalHintsShow()
    {
        var line = StatusBar.Render(AppState.Initial, 120, _keymap);

        Assert.DoesNotContain("browser", line);
        Assert.EndsWith("? help  : commands  q quit", line);
    }

    private static AppState ActiveOf(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static AppState With(
        ViewLayout layout,
        string? message = null,
        ViewSourceKind source = ViewSourceKind.Board,
        bool detail = false)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var tab = TabState.Opened(new TabId(1), new ViewDefinition(source, "4", layout, "Board"), 1, 1, 10);

        if (detail)
        {
            tab = tab with { Detail = [DetailState.Opened("A-1", null, 1, 2, 3)] };
        }

        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = session,
            Message = message,
        };
    }
}
