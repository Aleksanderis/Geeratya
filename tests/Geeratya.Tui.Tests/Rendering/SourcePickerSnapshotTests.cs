using Geeratya.Core.History;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
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
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPicker_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("g"), width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"picker-{width}x{height}.txt");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void GivenListsStillLoading_WhenBuildingTheFrame_ThenEachGroupSaysLoading()
    {
        var state = Sample("") with { Session = Sample("").Session with { Sources = SourceLists.Empty.Requested(SourceListKind.Boards) } };

        var lines = FrameBuilder.Build(state, 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Loading…", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTypedTextMatchingNothing_WhenBuildingTheFrame_ThenTheQueryRowIsOffered()
    {
        var lines = FrameBuilder.Build(Sample("zzz"), 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("zzz", StringComparison.Ordinal) && line.Contains("JQL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrameWithThePicker_ThenNoShellGlyphIsDrawn(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("g"), width, height, _helpLines, _keymap, null, GlyphSet.Ascii);

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

        var lines = FrameBuilder.Build(shown, 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Recent", StringComparison.Ordinal)
            && line.Contains("project = GCM", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Loading", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Boards", StringComparison.Ordinal));
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
