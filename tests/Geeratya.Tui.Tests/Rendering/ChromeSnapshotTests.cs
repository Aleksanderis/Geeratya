using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The chrome around the body (tab row, status bar with badge and hint chips) in every glyph set and colour tier
/// (<c>docs/internal/ux-look.md</c>, sections 5 and 6). To refresh the snapshots after an intended change, run with
/// <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class ChromeSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Theme _theme = BuiltInThemes.Default;
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<int, int, bool> Sizes
    {
        get
        {
            var data = new TheoryData<int, int, bool>();

            foreach (var ascii in new[] { false, true })
            {
                data.Add(120, 36, ascii);
                data.Add(80, 24, ascii);
                data.Add(40, 10, ascii);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void GivenThreeKindsOfTabs_WhenBuildingTheFrame_ThenTheChromeMatchesTheSnapshot(int width, int height, bool ascii)
    {
        var glyphs = ascii ? GlyphSet.Ascii : GlyphSet.Unicode;

        var lines = FrameBuilder.Build(State(), width, height, _keymap, null, glyphs);
        var chrome = $"{lines[0]}\n{lines[^1]}\n";
        var name = $"chrome-{(ascii ? "ascii-" : string.Empty)}{width}x{height}.txt";

        Assert.Equal(SnapshotFile.Expect(name, chrome), chrome);

        if (ascii)
        {
            Assert.All(chrome, character => Assert.True(character < 128, $"Non-ASCII '{character}' in the chrome"));
        }
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenIdenticalLabelsAndARenamedTab_WhenBuildingTheFrame_ThenTheTabRowMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(SameSourceState(), width, height, _keymap);
        var row = $"{lines[0]}\n";
        var name = $"chrome-tabs-same-{width}x{height}.txt";

        Assert.Equal(SnapshotFile.Expect(name, row), row);
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    public void GivenAColourTier_WhenResolvingTheBars_ThenChipsAreFilledAndTheBarsShareTheBarBackgroundExceptInactiveTabsAtTrueColour(ColorSupport tier)
    {
        var frame = FrameBuilder.BuildStyled(State(), 120, 36, _keymap)!;
        var tabs = StyledRuns.Resolve(frame.Header[0], _theme, tier);
        var status = StyledRuns.Resolve(frame.StatusStyled!, _theme, tier);
        var bar = Palette.Resolve(_theme, tier, PaletteRole.Status);
        var accent = Palette.Resolve(_theme, tier, PaletteRole.TabActive);

        var active = tabs.First(run => run.Text.Contains(" 2 JQL "));
        var dim = tabs.First(run => run.Text.Contains(" 1 Board "));
        var badge = status[0];

        Assert.Equal(accent, active.Style);
        Assert.NotEqual(bar.Background, active.Style.Background);
        Assert.True(active.Style.Style.HasFlag(RoleStyle.Bold));
        Assert.Equal(" NORMAL ", badge.Text);
        Assert.Equal(Palette.Resolve(_theme, tier, PaletteRole.BadgeNormal), badge.Style);
        Assert.Equal(bar.Background16, dim.Style.Background16);
        Assert.Equal(tier == ColorSupport.TrueColor, dim.Style.Background != bar.Background);
        Assert.Equal(bar.Background, status[^1].Style.Background);
        Assert.Equal(bar.Background16, status[^1].Style.Background16);
    }

    [Fact]
    public void GivenTheNoColourTier_WhenResolvingTheBars_ThenTheBarIsReversedAndChipsAreBoldWithoutReverse()
    {
        var frame = FrameBuilder.BuildStyled(State(), 120, 36, _keymap)!;
        var tabs = StyledRuns.Resolve(frame.Header[0], _theme, ColorSupport.None);
        var status = StyledRuns.Resolve(frame.StatusStyled!, _theme, ColorSupport.None);

        Assert.True(tabs.First(run => run.Text.Contains(" 1 Board ")).Style.Style.HasFlag(RoleStyle.Reverse));
        Assert.True(status[^1].Style.Style.HasFlag(RoleStyle.Reverse));
        Assert.Equal(RoleStyle.Bold, tabs.First(run => run.Text.Contains(" 2 JQL ")).Style.Style);
        Assert.Equal(RoleStyle.Bold, status[0].Style.Style);
    }

    [Theory]
    [InlineData(InputMode.Normal, TextRole.BadgeNormal)]
    [InlineData(InputMode.Insert, TextRole.BadgeInsert)]
    [InlineData(InputMode.Command, TextRole.BadgeCommand)]
    [InlineData(InputMode.Confirm, TextRole.BadgeConfirm)]
    [InlineData(InputMode.Help, TextRole.BadgeHelp)]
    public void GivenAMode_WhenBuildingTheStatusBar_ThenTheBadgeStartsAtColumnZeroInItsRole(InputMode mode, TextRole role)
    {
        var line = StatusBar.RenderStyled(FrameOptions.SampleState(mode), 80, _keymap);

        Assert.Equal(role, line.Spans[0].Role);
        Assert.StartsWith(" ", line.PlainText);
        Assert.Equal(80, line.PlainText.Length);
    }

    [Fact]
    public void GivenHints_WhenBuildingTheStatusBar_ThenEachKeyIsBoldAndEachLabelMuted()
    {
        var line = StatusBar.RenderStyled(State(), 120, _keymap);

        var keys = line.Spans.Where(span => span.Role == TextRole.BarKey).Select(span => span.Text).ToList();

        Assert.Contains("?", keys);
        Assert.Contains(":", keys);
        Assert.Contains("q", keys);
        Assert.Contains(line.Spans, span => span is { Role: TextRole.BarMuted, Text: " quit" });
    }

    [Theory]
    [InlineData(StatusSeverity.Error, TextRole.BarError)]
    [InlineData(StatusSeverity.Warning, TextRole.BarWarning)]
    public void GivenAMessage_WhenBuildingTheStatusBar_ThenItIsDrawnInItsSeverityRoleOnTheBar(
        StatusSeverity severity,
        TextRole role)
    {
        var state = new AppReducer(_keymap)
            .Reduce(State() with { Message = "Saved A-1", MessageSeverity = severity }, new Tick(_now))
            .State;

        var line = StatusBar.RenderStyled(state, 100, _keymap, _now);

        Assert.Contains(line.Spans, span => span.Role == role && span.Text.EndsWith("Saved A-1", StringComparison.Ordinal));
    }

    private static AppState SameSourceState()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GEN", ViewLayout.List, "project = GEN");
        var tabs = Enumerable.Range(1, 3)
            .Select(index => TabState.Opened(new TabId(index), definition, index, index, 10) with { Load = LoadState.Idle })
            .ToList();

        tabs[2] = tabs[2] with { Name = "Release 12" };

        var session = SiteSession.Empty(profile) with { Tabs = tabs, Active = tabs[1].Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static AppState State()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var kinds = new[]
        {
            new ViewDefinition(ViewSourceKind.Board, "10", ViewLayout.Columns, "GCM board"),
            new ViewDefinition(ViewSourceKind.Jql, "project = GEN", ViewLayout.List, "project = GEN"),
            new ViewDefinition(ViewSourceKind.Backlog, "10", ViewLayout.List, "GEN board"),
        };
        var tabs = kinds
            .Select((definition, index) => TabState.Opened(new TabId(index + 1), definition, index, index, 10)
                with { Load = LoadState.Idle })
            .ToList();
        var session = SiteSession.Empty(profile) with { Tabs = tabs, Active = tabs[1].Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }
}
