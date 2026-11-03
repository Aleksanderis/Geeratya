using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The command palette drawn at the snapshot sizes, its clickable rows and its edge cases.</summary>
[Trait("Os", "Independent")]
public class PaletteOverlayTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData("", 0, 120, 36)]
    [InlineData("", 0, 80, 24)]
    [InlineData("", 0, 40, 10)]
    [InlineData("rank", 0, 80, 24)]
    [InlineData("jql project", 0, 80, 24)]
    [InlineData("zzqx", 0, 80, 24)]
    public void GivenAnOpenPalette_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(
        string text,
        int selected,
        int width,
        int height)
    {
        var state = Sample(text) with { PaletteSelected = selected };
        var actual = string.Join('\n', FrameBuilder.Build(state, width, height, _keymap)) + "\n";
        var name = $"palette-{Slug(text)}-{width}x{height}.txt";
        var expected = SnapshotFile.Expect(name, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GivenThemeNames_WhenBuildingTheFrame_ThenThemeRowsMatchTheSnapshot()
    {
        var state = Sample("theme:") with { ThemeNames = ["catppuccin-mocha", "dracula", "nord"] };
        var actual = string.Join('\n', FrameBuilder.Build(state, 80, 24, _keymap)) + "\n";
        var expected = SnapshotFile.Expect("palette-themes-80x24.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GivenSeveralGroupsThatFit_WhenBuildingTheFrame_ThenABlankLinePrecedesEverySubheaderButTheFirst()
    {
        var inner = BoxInner(FrameBuilder.Build(Sample("the tab"), 100, 36, _keymap));
        var headers = inner.Select((text, index) => new { text, index })
            .Where(item => ActionCatalog.Groups.Contains(item.text))
            .ToList();

        Assert.True(headers.Count >= 2);
        Assert.Equal(0, headers[0].index);
        Assert.All(headers.Skip(1), header => Assert.Equal(string.Empty, inner[header.index - 1]));
    }

    [Fact]
    public void GivenTheFullList_WhenItDoesNotFit_ThenTheBlankLinesAreDropped()
    {
        var inner = BoxInner(FrameBuilder.Build(Sample(string.Empty), 100, 36, _keymap));

        Assert.DoesNotContain(string.Empty, inner);
        Assert.Contains(ActionCatalog.Groups[0], inner);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPalette_WhenReadingTheRegions_ThenEveryRowIsClickableAndSubheadersAreNot(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample(string.Empty), width, height, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.PaletteRow);

        Assert.NotEmpty(rows);
        Assert.Equal(Enumerable.Range(0, rows.Count), rows.Select(row => row.Index));
        Assert.Equal(rows.Count, rows.Select(row => row.Region.Top).Distinct().Count());
        Assert.All(rows, row => Assert.DoesNotContain(row.Text.Trim().Trim('│').Trim(), ActionCatalog.Groups));
        Assert.All(rows, row => Assert.InRange(row.Region.Top, FrameBuilder.HeaderRows, height - 2));
    }

    [Fact]
    public void GivenAFilteredPalette_WhenClickingARow_ThenThatActionRunsAfterThePaletteClosed()
    {
        var state = Sample("hide help");
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.PaletteRow).Single();

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Contains("Show or hide help", row.Text, StringComparison.Ordinal);
        Assert.Equal(InputMode.Help, result.State.Mode);
        Assert.Equal(string.Empty, result.State.PaletteText);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenWheelingOverARow_ThenTheHighlightMovesAndThePaletteStaysOpen()
    {
        var state = Sample(string.Empty);
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.PaletteRow)[0];

        var down = RegionProbe.Click(state, frame, row.Region, MouseGesture.WheelDown)!;
        var up = RegionProbe.Click(down.State, frame, row.Region, MouseGesture.WheelUp)!;

        Assert.Equal(3, down.State.PaletteSelected);
        Assert.Equal(0, up.State.PaletteSelected);
        Assert.Equal(InputMode.Command, up.State.Mode);
    }

    [Fact]
    public void GivenASelectedRowFarDown_WhenBuildingTheFrame_ThenTheListScrollsToShowIt()
    {
        var count = PaletteRows.Selectable(Sample(string.Empty), _keymap).Count;
        var state = Sample(string.Empty) with { PaletteSelected = count - 1 };

        var frame = FrameBuilder.BuildStyled(state, 80, 12, _keymap)!;
        var marked = frame.PlainLines().Where(line => line.Contains("│ > ", StringComparison.Ordinal)).ToList();

        Assert.Single(marked);
        Assert.Equal(count - 1, RegionProbe.Rows(frame, MouseTarget.PaletteRow).Max(row => row.Index));
    }

    [Fact]
    public void GivenARowWithKeys_WhenBuildingTheFrame_ThenTheKeysShowAtTheEnd()
    {
        var lines = FrameBuilder.Build(Sample("Close the tab"), 100, 24, _keymap);
        var row = lines.Single(line => line.Contains("│ > Close the tab", StringComparison.Ordinal));

        Assert.Contains("Space c", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrame_ThenNoUnicodeGlyphLeaks()
    {
        var frame = FrameBuilder.Build(Sample(string.Empty), 80, 24, _keymap, null, GlyphSet.Ascii);

        Assert.All(frame, line => Assert.All(line, character => Assert.True(character < 128, line)));
    }

    [Fact]
    public void GivenAnySizeAndFilter_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        foreach (var text in new[] { string.Empty, "rank", "jql x", "zzqx", "😀" })
        {
            for (var width = 0; width <= 30; width++)
            {
                for (var height = 0; height <= 12; height++)
                {
                    FrameBuilder.BuildStyled(Sample(text), width, height, _keymap);
                }
            }
        }
    }

    [Fact]
    public void GivenAFilter_WhenBuildingTheFrame_ThenTheTopEdgeCarriesTheCountAndTheQueryLineTheText()
    {
        var lines = FrameBuilder.Build(Sample("rank"), 80, 24, _keymap);
        var top = lines.First(line => line.Contains("╭─ Commands", StringComparison.Ordinal));
        var query = lines.First(line => line.Contains(": rank_", StringComparison.Ordinal));

        Assert.Matches(@"Commands ─+ 6/\d+ ─╮", top);
        Assert.Contains("│ : rank_", query, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATypedCommandLine_WhenBuildingTheFrame_ThenNoCountChipIsShown()
    {
        var lines = FrameBuilder.Build(Sample("jql project"), 80, 24, _keymap);
        var top = lines.First(line => line.Contains("╭─ Commands", StringComparison.Ordinal));

        Assert.DoesNotMatch(@"\d+/\d+", top);
    }

    [Fact]
    public void GivenTheStyledFrame_WhenReadingTheRoles_ThenSubheaderIsAccentSelectedRowIsTintedAndCountIsDim()
    {
        var frame = FrameBuilder.BuildStyled(Sample(string.Empty), 80, 24, _keymap)!;
        var spans = frame.Body.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.Heading && span.Text.Trim() == "Issue");
        Assert.Contains(spans, span => span.Role == TextRole.Selected && span.Text.Contains("> Jump", StringComparison.Ordinal));
        Assert.Contains(spans, span => span.Role == TextRole.OverlayCount && span.Text.Contains('/', StringComparison.Ordinal));
    }

    private static List<string> BoxInner(IReadOnlyList<string> lines)
    {
        var all = lines.ToList();
        var top = all.FindIndex(line => line.Contains("╭─ Commands", StringComparison.Ordinal));
        var bottom = all.FindIndex(top + 1, line => line.Contains('╰', StringComparison.Ordinal));
        var left = all[top].IndexOf('╭', StringComparison.Ordinal);
        var right = all[bottom].IndexOf('╯', StringComparison.Ordinal);

        // The list between the walls of the box, without the query line, the rules and the footer.
        var rows = all.Skip(top + 3).Take(bottom - top - 3);

        return
        [
            .. rows.Select(line => line[(left + 1)..right].Trim().Trim('│').Trim())
                .TakeWhile(text => !text.StartsWith("──", StringComparison.Ordinal)),
        ];
    }

    private static string Slug(string text)
    {
        return text.Length == 0 ? "all" : text.Replace(' ', '-');
    }

    internal static AppState Sample(string text)
    {
        var reducer = new AppReducer(_keymap);
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "View");
        var opened = reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs.Single();
        var loaded = reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([], null, 0), _now));

        return loaded.State with { Mode = InputMode.Command, PaletteText = text };
    }
}
