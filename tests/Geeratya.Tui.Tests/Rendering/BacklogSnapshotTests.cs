using Geeratya.Core.Text;
using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The Scrum backlog sections at the three snapshot sizes, expanded and with a collapsed section, plus the ASCII
/// glyph set. To refresh after an intended change, copy the actual frame from the failure into the matching file and
/// review the diff.
/// </summary>
public class BacklogSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    private static readonly Sprint _active = new(
        7,
        "Sprint 1",
        SprintState.Active,
        "Ship the login 日本語",
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero),
        null,
        6);

    private static readonly Sprint _future = new(8, "Sprint 2", SprintState.Future, null, null, null, null, 6);
    private static readonly Sprint _empty = new(9, "Sprint 3", SprintState.Future, null, null, null, null, 6);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenALoadedBacklog_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(false), width, height, _keymap);

        var actual = Actual(lines);

        Assert.Equal(SnapshotFile.Expect($"backlog-{width}x{height}.txt", actual), actual);
    }

    [Fact]
    public void GivenACollapsedSectionUnderTheCursor_WhenBuildingTheFrame_ThenItsHeaderCarriesTheCursor()
    {
        var lines = FrameBuilder.Build(Sample(true), 80, 24, _keymap);

        var actual = Actual(lines);

        Assert.Equal(SnapshotFile.Expect("backlog-collapsed-80x24.txt", actual), actual);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrame_ThenTheBodyMatchesTheAsciiSnapshot()
    {
        var lines = FrameBuilder.Build(Sample(true), 60, 14, _keymap, null, GlyphSet.Ascii)
            .Skip(FrameBuilder.HeaderRows);

        var actual = Actual(lines);

        Assert.Equal(SnapshotFile.Expect("backlog-ascii-60x14.txt", actual), actual);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 2)]
    [InlineData(20, 3)]
    [InlineData(5, 30)]
    public void GivenTinyTerminals_WhenBuildingTheFrame_ThenItDoesNotThrow(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(false), width, height, _keymap);

        Assert.NotNull(lines);
    }

    [Theory]
    [InlineData(false, GlyphSet.Unicode)]
    [InlineData(true, GlyphSet.Unicode)]
    [InlineData(false, GlyphSet.Ascii)]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenEveryLineKeepsTheWidthAndTheFrameCloses(
        bool collapsedFirst,
        GlyphSet glyphs)
    {
        for (var width = 1; width <= 130; width++)
        {
            for (var height = 1; height <= 14; height++)
            {
                var lines = FrameBuilder.Build(Sample(collapsedFirst), width, height, _keymap, null, glyphs);

                Assert.Equal(height, lines.Count);
                Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
            }
        }
    }

    [Fact]
    public void GivenSections_WhenRendered_ThenTheSharedEdgesCarryTheSprintChipsAndTheFrameHasRoundedCorners()
    {
        var body = FrameBuilder.Build(Sample(false), 100, 24, _keymap).Skip(FrameBuilder.HeaderRows).ToList();

        Assert.StartsWith("╭─ Scrum board", body[0]);
        Assert.EndsWith("─╮", body[0]);
        Assert.Contains(body, line => line.StartsWith("├─ Sprint 1 · active") && line.EndsWith("─┤"));
        Assert.Contains(body, line => line.StartsWith("├─ Backlog") && line.Contains("1 issue"));
        Assert.StartsWith("╰", body[^2]);
        Assert.DoesNotContain(body, line => line.StartsWith('┌') || line.StartsWith('┐'));
    }

    [Fact]
    public void GivenSections_WhenRendered_ThenEachHeaderTellsNameDatesCountAndPoints()
    {
        var layout = BacklogSections.Build(SampleTab(false));
        var glyphs = Glyphs.For(GlyphSet.Unicode);

        var texts = layout.Sections.Select(section => BacklogBodyRenderer.HeaderText(section, false, glyphs)).ToArray();

        Assert.Equal("- Sprint 1 · active · 2 Nov - 16 Nov · 2 issues · 8 pts", texts[0]);
        Assert.Equal("- Sprint 2 · no dates · 2 issues · 3 pts", texts[1]);
        Assert.Equal("- Sprint 3 · no dates · 0 issues", texts[2]);
        Assert.Equal("- Backlog · 1 issue · 1.5 pts", texts[3]);
    }

    [Fact]
    public void GivenMoreRowsComing_WhenTheHeaderIsBuilt_ThenCountsCarryAPlus()
    {
        var section = BacklogSections.Build(SampleTab(false)).Sections[3];

        var text = BacklogBodyRenderer.HeaderText(section, true, Glyphs.For(GlyphSet.Ascii));

        Assert.Equal("- Backlog - 1+ issues - 1.5+ pts", text);
    }

    private static string Actual(IEnumerable<string> lines)
    {
        return string.Join('\n', lines) + "\n";
    }

    private static TabState SampleTab(bool collapsedFirst)
    {
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board");
        var rows = BacklogSections.Order(
        [
            Row("GCS-5", null, 1.5, "Tidy the release notes"),
            Row("GCS-2", _future, 3, "Reporting overhaul with a very long summary that never fits on one line"),
            Row("GCS-3", _active, 5, "Fix login redirect after token refresh"),
            Row("GCS-4", _active, 3, "As a user I can filter by label 日本語ラベル"),
            Row("GCS-6", _future, null, "Crash when the board has no columns"),
        ]);

        return TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Rows = rows,
            Selected = 0,
            SelectedKey = "GCS-3",
            KeyWidth = 5,
            Load = LoadState.Idle,
            RequestId = 0,
            Sprints = [_active, _future, _empty],
            CollapsedSections = collapsedFirst ? [BacklogSections.SprintKey(7)] : [],
        };
    }

    private static AppState Sample(bool collapsedFirst)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var tab = SampleTab(collapsedFirst);
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Row(string key, Sprint? sprint, double? estimate, string summary)
    {
        IssueSprint[] sprints = sprint is null ? [] : [new IssueSprint(sprint.Id, sprint.Name, sprint.State)];

        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Story", 0, false),
            null,
            new IssueAssignee("a", "Jane Doe"),
            [],
            null,
            null,
            "GCS")
        {
            Sprints = sprints,
            Estimate = estimate,
        };
    }
}
