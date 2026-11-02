using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The list layout at the three snapshot sizes, with wide characters and a long summary. To refresh after an
/// intended change, copy the actual frame from the failure into the matching file and review the diff.
/// </summary>
public class ListSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenALoadedList_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(), width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var name = $"list-{width}x{height}.txt";

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name));

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrame_ThenTheBodyMatchesTheAsciiSnapshot()
    {
        var lines = FrameBuilder.Build(Sample(), 40, 10, _helpLines, _keymap, null, GlyphSet.Ascii)
            .Skip(FrameBuilder.HeaderRows);
        var actual = string.Join('\n', lines) + "\n";

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", "list-ascii-40x10.txt"));

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    private static AppState Sample()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "assignee = currentUser()", ViewLayout.List, "My open issues");
        var rows = new[]
        {
            Row("GCM-12", "Task", 0, "Fix login redirect after token refresh", "In Progress", "indeterminate", "Jane Doe"),
            Row("GCM-14", "Bug", 0, "Crash when the board has no columns", "To Do", "new", "Ola Nordmann"),
            Row("GCM-15", "Story", 0, "As a user I can filter by label 日本語ラベル", "To Do", "new", null),
            Row("GCM-17", "Epic", 1, "Reporting overhaul with a very long summary that never fits on one line", "In Progress", "indeterminate", "Jane Doe"),
            Row("GCM-18", "Sub-task", -1, "Write the CSV exporter 😀 é", "Done", "done", "Ola Nordmann"),
            Row("GCM-21", "Incident", 0, "Upgrade the build image", "Backlog", "new", null),
            Row("GCM-22", "Bug", 0, "Tooltip overlaps the header on small screens", "To Do", "new", "Jane Doe"),
            Row("GCM-23", "Task", 0, "Document the release checklist", "Done", "done", "Ola Nordmann"),
            Row("GCM-25", "Story", 0, "As an admin I can see who changed a setting", "In Progress", "indeterminate", "Jane Doe"),
            Row("GCM-26", "Task", 0, "Remove the legacy export flag", "To Do", "new", null),
        };

        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Rows = rows,
            Selected = 2,
            SelectedKey = "GCM-15",
            KeyWidth = 6,
            Next = new PageCursor("n2"),
            Load = new Loading(LoadReason.NextPage),
        };
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Row(
        string key,
        string type,
        int level,
        string summary,
        string status,
        string category,
        string? assignee)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus(status, status, category),
            new IssueTypeSummary(type, type, level, type == "Sub-task"),
            null,
            assignee is null ? null : new IssueAssignee(assignee, assignee),
            [],
            null,
            null,
            "GCM");
    }
}
