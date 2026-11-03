using Geeratya.Core.History;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The source picker at the three snapshot sizes, with wide characters and a long name. To refresh after an
/// intended change, copy the actual frame from the failure into the matching file and review the diff.
/// </summary>
public class SourcePickerSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPicker_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("g"), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"picker-{width}x{height}.txt");

        Assert.Equal(File.ReadAllText(path), actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPicker_WhenReadingTheRegions_ThenEachRowCoversItsChoiceWithinTheBox(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample(string.Empty), width, height, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.PickerRow);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.InRange(row.Region.Top, FrameBuilder.HeaderRows, height - 2));
        Assert.All(rows, row => Assert.InRange(row.Region.Left + row.Region.Width, 1, width));
        Assert.Equal(rows.Count, rows.Select(row => row.Region.Top).Distinct().Count());
        Assert.Contains(rows, row => row.Text.Contains("GCM board", StringComparison.Ordinal) && row.Index == 0);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenClickingAFilterChoice_ThenTheFilterOpensInANewTab()
    {
        var state = Sample(string.Empty);
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.PickerRow).Single(candidate => candidate.Index == 4);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Contains("My open bugs", row.Text);
        Assert.Null(result.State.Picker);
        Assert.Equal("My open bugs", Assert.Single(result.State.Session.Tabs).Definition.Title);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenClickingOutsideTheRows_ThenNothingIsResolved()
    {
        var state = Sample(string.Empty);
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var context = MouseContext.From(state, 80, 24, FrameBuilder.HeaderRows, default, frame.Regions);

        Assert.Empty(MouseResolver.Resolve(context, 0, 20, MouseGesture.Click, default));
    }

    [Fact]
    public void GivenListsStillLoading_WhenBuildingTheFrame_ThenEachGroupSaysLoading()
    {
        var state = Sample("") with { Session = Sample("").Session with { Sources = SourceLists.Empty.Requested(SourceListKind.Boards) } };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Loading…", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTypedTextMatchingNothing_WhenBuildingTheFrame_ThenTheQueryRowIsOffered()
    {
        var lines = FrameBuilder.Build(Sample("zzz"), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("zzz", StringComparison.Ordinal) && line.Contains("JQL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrameWithThePicker_ThenNoShellGlyphIsDrawn(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("g"), width, height, _keymap, null, GlyphSet.Ascii);

        Assert.Contains(lines, line => line.Contains('+', StringComparison.Ordinal));
        Assert.All(lines, line => Assert.DoesNotContain(line, character => Glyphs.AsciiSwapped.Contains(character)));
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }

    [Fact]
    public void GivenTheJqlPickerWithHistory_WhenBuildingTheFrame_ThenRecentQueriesShowWithoutBoardGroups()
    {
        var state = Sample("") with { Picker = new SourcePickerState(string.Empty, 0, PickerScope.Jql, false, null) };
        var lists = state.Session.Sources with { Boards = null, History = SiteHistory.Empty.WithQuery("project = GCM") };
        var shown = state with { Session = state.Session with { Sources = lists } };

        var lines = FrameBuilder.Build(shown, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Recent", StringComparison.Ordinal)
            && line.Contains("project = GCM", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Loading", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Boards", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenSavedViews_WhenBuildingThePickerFrames_ThenTheyShowUnderTheirOwnHeadingWithoutBoards()
    {
        var sample = Sample(string.Empty);
        var views = new SavedViewList([new SavedView("Team board", "Board", "4", "Columns")]);
        var lists = sample.Session.Sources with { SavedViews = views };
        var all = sample with { Session = sample.Session with { Sources = lists } };
        var only = all with { Picker = new SourcePickerState(string.Empty, 0, PickerScope.Views, false, null) };

        var allText = string.Join('\n', FrameBuilder.Build(all, 100, 30, _keymap));
        var onlyText = string.Join('\n', FrameBuilder.Build(only, 100, 30, _keymap));

        Assert.Contains("Saved views", allText);
        Assert.Contains("Team board (board, columns)", allText);
        Assert.Contains("Team board (board, columns)", onlyText);
        Assert.DoesNotContain("GCM board", onlyText);
    }

    private static AppState Sample(string text)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var lists = SourceLists.Empty with
        {
            Boards =
            [
                new BoardSummary(4, "GCM board", BoardType.Scrum, "GCM"),
                new BoardSummary(3, "GTM 日本語 board with a rather long name that cannot fit", BoardType.Kanban, "GTM"),
            ],
            Filters =
            [
                new SavedFilter("10001", "My open bugs", "type = Bug", "Jane Doe", true, null),
                new SavedFilter("10003", "Filter for GCM board", "project = GCM", "Jane Doe", false, null),
            ],
        };
        var session = SiteSession.Empty(profile) with { Sources = lists };

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = session,
            Picker = new SourcePickerState(text, 0, PickerScope.All, false, null),
        };
    }
}
