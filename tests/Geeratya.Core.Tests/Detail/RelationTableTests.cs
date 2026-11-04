using Geeratya.Core.Detail;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>The aligned table of the Sub-tasks, Child issues and Linked issues panes (#702).</summary>
[Trait("Os", "Independent")]
public class RelationTableTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueStatus _progress = new("2", "In Progress", "indeterminate");
    private static readonly IssueStatus _done = new("3", "Done", "done");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueTypeSummary _subtask = new("12", "Sub-task", -1, true);

    [Theory]
    [InlineData(64)]
    [InlineData(80)]
    [InlineData(104)]
    [InlineData(120)]
    public void GivenSubtasksWithAssignees_WhenBuilding_ThenKeyStatusAndAssigneeColumnsAlignAtEveryWidth(int width)
    {
        var rows = SubtaskRows(Build(WithAssignees(), width));

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.StartsWith("GEN-100", row.Text));
        Assert.Single(rows.Select(row => row.Text.IndexOf("  ", 10, StringComparison.Ordinal)).Distinct());
        Assert.Single(rows.Select(row => ColumnOf(row, "[G")).Where(column => column >= 0).Distinct());
        Assert.Single(rows.Select(row => ColumnOf(row, "[")).Distinct());
        Assert.Contains(rows, row => row.Text.Contains("[GW]"));
        Assert.Contains(rows, row => row.Text.Contains("[--]"));

        // At 64 columns the names give way to the chips; from 80 columns on they fit.
        Assert.Equal(width >= 80, rows.Any(row => row.Text.Contains("[GW] Generated Writer")));
        Assert.Equal(width >= 80, rows.Any(row => row.Text.Contains("[--] Unassigned")));
    }

    [Fact]
    public void GivenAShortKeyAmongLongOnes_WhenBuilding_ThenTheKeyColumnIsAsWideAsTheLongestKey()
    {
        var issue = Issue() with
        {
            Subtasks =
            [
                new IssueStub("GEN-9", "Short key", _todo, _subtask),
                new IssueStub("GEN-100051", "Long key", _todo, _subtask),
            ],
        };

        var rows = SubtaskRows(Build(Detail(issue), 80));

        Assert.StartsWith("GEN-9       Short key", rows[0].Text);
        Assert.StartsWith("GEN-100051  Long key", rows[1].Text);
    }

    [Fact]
    public void GivenALongSummary_WhenBuilding_ThenItIsCutWithAnEllipsisAndTheStatusNeverTouchesIt()
    {
        var issue = Issue() with
        {
            Subtasks = [new IssueStub("GEN-1", new string('x', 200), _progress, _subtask)],
        };

        var row = Assert.Single(SubtaskRows(Build(Detail(issue), 64)));

        Assert.Contains("…  In Progress", row.Text);
        Assert.Equal(1, row.Text.Count(character => character == '…'));
    }

    [Fact]
    public void GivenNarrowWidths_WhenBuilding_ThenTheAssigneeNameGoesFirstThenTheChipThenTheStatus()
    {
        var wide = Assert.Single(SubtaskRows(Build(WithAssignees(1), 80)));
        var chipOnly = Assert.Single(SubtaskRows(Build(WithAssignees(1), 56)));
        var noChip = Assert.Single(SubtaskRows(Build(WithAssignees(1), 40)));

        Assert.Contains("[GW] Generated Writer", wide.Text);
        Assert.Contains("[GW]", chipOnly.Text);
        Assert.DoesNotContain("Generated", chipOnly.Text);
        Assert.Contains("In Progress", chipOnly.Text);
        Assert.DoesNotContain("[GW]", noChip.Text);
        Assert.DoesNotContain("In Progress", noChip.Text);
        Assert.StartsWith("GEN-100051", noChip.Text);
    }

    [Fact]
    public void GivenAssigneeRoles_WhenBuilding_ThenInitialsAreAChipAndUnassignedIsMuted()
    {
        var spans = SubtaskRows(Build(WithAssignees(), 104)).SelectMany(row => row.Line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.InitialsChip && span.Text == "[GW]");
        Assert.Contains(spans, span => span.Role == TextRole.UnassignedChip && span.Text == "[--]");
        Assert.Contains(spans, span => span.Role == TextRole.Muted && span.Text.StartsWith("Unassigned"));
        Assert.Contains(spans, span => span.Role == TextRole.StatusInProgress && span.Text.StartsWith("In Progress"));
        Assert.Contains(spans, span => span.Role == TextRole.StatusDone && span.Text.StartsWith("Done"));
    }

    [Fact]
    public void GivenSubtasksBeforeTheChildrenSearchArrives_WhenBuilding_ThenTheAssigneeCellIsBlankNotUnassigned()
    {
        var detail = WithAssignees() with { Children = null };

        var rows = SubtaskRows(Build(detail, 104));

        Assert.All(rows, row => Assert.DoesNotContain("Unassigned", row.Text));
        Assert.All(rows, row => Assert.DoesNotContain("[", row.Text));
    }

    [Fact]
    public void GivenSubtasks_WhenBuilding_ThenThePaneChipCountsTheDoneOnes()
    {
        var document = Build(WithAssignees(), 104);
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Subtasks);

        Assert.Equal("1 of 3 done", string.Concat(document.PaneRightTitles[pane].Select(span => span.Text)));
        Assert.Equal("Sub-tasks 3", string.Concat(document.PaneTitles[pane].Select(span => span.Text)));
    }

    [Fact]
    public void GivenAStoryWithSubtasks_WhenTheChildrenArrive_ThenNoChildIssuesPaneShows()
    {
        var document = Build(WithAssignees(), 104);

        Assert.DoesNotContain(PaneIds.Children, document.SectionIds);
    }

    [Fact]
    public void GivenALinkedIssueRow_WhenBuilding_ThenItUsesTheSameTableWithTheDirectionBeforeTheSummary()
    {
        var link = new IssueLink(
            "1",
            "Blocks",
            IssueLinkDirection.Outward,
            "blocks",
            new IssueStub("GEN-30", "Launch", _todo, _story));
        var detail = Detail(Issue() with { Links = [link] });

        var document = Build(detail, 80);
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Links);
        var row = document.Lines[document.Sections[pane]].PlainText;

        Assert.StartsWith("│ GEN-30  blocks Launch", row);
        Assert.Contains("To Do", row);
    }

    [Fact]
    public void GivenWideCharactersInNames_WhenBuilding_ThenNoRowIsWiderThanThePane()
    {
        var issue = Issue() with
        {
            Subtasks = [new IssueStub("GEN-1", "日本語のとても長いサブタスクの題名がここに入ります", _todo, _subtask)],
        };
        var detail = Detail(issue) with
        {
            Children = [Row("GEN-1", "日本語の担当者", "日本語 作者")],
        };

        foreach (var width in new[] { 40, 64, 80, 120 })
        {
            var document = Build(detail, width);

            var main = DetailLayout.HasSidebar(width) ? DetailLayout.MainWidth(width) : width;

            Assert.All(document.Lines, line => Assert.Equal(main, DisplayWidth.Of(line.PlainText)));
        }
    }

    private static int ColumnOf(SubtaskRow row, string text)
    {
        var index = row.Text.IndexOf(text, StringComparison.Ordinal);

        return index < 0 ? -1 : DisplayWidth.Of(row.Text[..index]);
    }

    private static List<SubtaskRow> SubtaskRows(DetailDocument document)
    {
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Subtasks);
        var end = pane + 1 < document.PaneStarts.Count ? document.PaneStarts[pane + 1] - 1 : document.Lines.Count - 1;

        return
        [
            .. Enumerable.Range(document.Sections[pane], end - document.Sections[pane])
                .Select(line => new SubtaskRow(document.Lines[line], Inside(document.Lines[line].PlainText))),
        ];
    }

    private static string Inside(string text)
    {
        return text.StartsWith("│ ") && text.EndsWith(" │") ? text[2..^2].TrimEnd() : text;
    }

    private static DetailDocument Build(DetailState detail, int width)
    {
        return IssueDocumentBuilder.Build(detail, width);
    }

    private static DetailState WithAssignees(int take = 3)
    {
        var issue = Issue() with
        {
            Subtasks =
            [
                .. new[]
                {
                    new IssueStub("GEN-100051", "Sub-task 1 of 5", _progress, _subtask),
                    new IssueStub("GEN-100052", "Sub-task 2 of 5", _done, _subtask),
                    new IssueStub("GEN-100053", "Sub-task 3 of 5", _todo, _subtask),
                }.Take(take),
            ],
        };

        return Detail(issue) with
        {
            Children =
            [
                Row("GEN-100051", "Sub-task 1 of 5", "Generated Writer"),
                Row("GEN-100052", "Sub-task 2 of 5", "Generated Reporter"),
                Row("GEN-100053", "Sub-task 3 of 5", null),
            ],
        };
    }

    private static DetailState Detail(IssueDetail issue)
    {
        return DetailState.Opened("GEN-5", null, 0, 0, 0) with
        {
            Issue = issue,
            IssueLoad = LoadState.Idle,
            CommentsLoad = LoadState.Idle,
            NextComments = null,
            LinksLoad = LoadState.Idle,
            RemoteLinks = [],
            LoadedAt = _now,
        };
    }

    private static IssueSummary Row(string key, string summary, string? assignee)
    {
        return new IssueSummary(
            "1",
            key,
            summary,
            _todo,
            _subtask,
            null,
            assignee is null ? null : new IssueAssignee(assignee, assignee),
            [],
            null,
            null,
            "GEN");
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "GEN-5",
            "Generated issue 5",
            _todo,
            _story,
            null,
            null,
            null,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "GEN",
            null,
            null,
            [],
            [],
            []);
    }

    private sealed record SubtaskRow(StyledLine Line, string Text);
}
