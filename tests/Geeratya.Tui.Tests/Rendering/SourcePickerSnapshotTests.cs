using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;
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
