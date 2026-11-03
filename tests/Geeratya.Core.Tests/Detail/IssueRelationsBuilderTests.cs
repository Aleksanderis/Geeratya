using Geeratya.Core.Detail;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Detail;

[Trait("Os", "Independent")]
public class IssueRelationsBuilderTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly char _control = (char)27;
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueStatus _done = new("2", "Done", "done");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueTypeSummary _epic = new("11", "Epic", 1, false);
    private static readonly IssueTypeSummary _subtask = new("12", "Sub-task", -1, true);

    [Fact]
    public void GivenIssueWithoutRelations_WhenBuilding_ThenNoRelationPanelIsDrawn()
    {
        var lines = Panels(Build(Detail(Issue())));

        Assert.DoesNotContain(lines, title => title.StartsWith("Sub-tasks"));
        Assert.DoesNotContain(lines, title => title.StartsWith("Linked issues"));
        Assert.DoesNotContain(lines, title => title.StartsWith("Attachments"));
        Assert.DoesNotContain(lines, title => title.StartsWith("Remote links"));
        Assert.DoesNotContain(lines, title => title.StartsWith("Epic children"));
    }

    [Fact]
    public void GivenEveryRelation_WhenBuilding_ThenPanelsFollowTheDocumentedOrderWithCounts()
    {
        var detail = Detail(Epic()) with
        {
            Children = [Row("A-21", "Child")],
            RemoteLinks = [new RemoteLink("1", "https://example.com/a", "Spec", "Confluence")],
        };

        var titles = Panels(Build(detail));

        Assert.Equal(
            [
                "A-1 · Epic",
                "Description",
                "Sub-tasks 1",
                "Epic children 1",
                "Linked issues 2",
                "Remote links 1",
                "Attachments 1",
                "Comments",
            ],
            titles);
    }

    [Fact]
    public void GivenLinksInBothDirections_WhenBuilding_ThenEachReadsWithItsOwnDirectionText()
    {
        var text = Text(Build(Detail(Epic())));

        Assert.Contains("blocks A-30 Launch · To Do", text);
        Assert.Contains("is blocked by A-9 Legal review · Done", text);
    }

    [Fact]
    public void GivenLinkTypeWithoutDirectionWording_WhenBuilding_ThenTheTypeNameStandsInForIt()
    {
        var link = new IssueLink("1", "Relates", IssueLinkDirection.Outward, string.Empty, Epic().Links[0].Issue);
        var detail = Detail(Epic() with { Links = [link] });

        Assert.Contains("Relates A-30 Launch", Text(Build(detail)));
    }

    [Fact]
    public void GivenSubtasks_WhenBuilding_ThenKeySummaryAndStatusShowWithTheStatusColourRole()
    {
        var document = Build(Detail(Epic()));

        var line = document.Lines.Single(candidate => candidate.PlainText.Contains("A-18 Write the migration"));
        Assert.Contains(line.Spans, span => span.Role == TextRole.Key && span.Text == "A-18");
        Assert.Contains(line.Spans, span => span.Role == TextRole.StatusToDo && span.Text.Contains("To Do"));
    }

    [Fact]
    public void GivenChildrenCutAtTheFirstPage_WhenBuilding_ThenTheTitleAndNoteSaySo()
    {
        var detail = Detail(Epic()) with { Children = [Row("A-21", "Child")], ChildrenMore = true };

        var text = Text(Build(detail));

        Assert.Contains("Epic children 1+", text);
        Assert.Contains("Showing the first children only.", text);
    }

    [Fact]
    public void GivenChildrenLoading_WhenBuilding_ThenThePanelShowsLoadingAndGoesAwayWhenEmpty()
    {
        var loading = Detail(Epic()) with { ChildrenLoad = new Loading(LoadReason.Open) };
        var empty = Detail(Epic()) with { Children = [] };

        Assert.Contains("Loading…", Section(Build(loading), "Epic children"));
        Assert.DoesNotContain("Epic children", Text(Build(empty)));
    }

    [Fact]
    public void GivenChildrenFailed_WhenBuilding_ThenTheErrorShowsInlineAndTheOtherPanelsStay()
    {
        var failed = Detail(Epic()) with
        {
            ChildrenLoad = new Failed(JiraErrorKind.Forbidden, "No permission to search."),
        };

        var document = Build(failed);

        Assert.Contains("No permission to search.", Section(document, "Epic children"));
        Assert.Contains("Linked issues 2", Text(document));
        Assert.Contains("Attachments 1", Text(document));
        Assert.Contains(
            document.Lines.SelectMany(line => line.Spans),
            span => span.Role == TextRole.Error && span.Text == "No permission to search.");
    }

    [Fact]
    public void GivenRemoteLinksFailedOrLoading_WhenBuilding_ThenTheirPanelShowsItAndNothingElseChanges()
    {
        var failed = Detail(Epic()) with { LinksLoad = new Failed(JiraErrorKind.Network, "Network down.") };
        var loading = Detail(Epic()) with { LinksLoad = new Loading(LoadReason.Open) };

        Assert.Contains("Network down.", Section(Build(failed), "Remote links"));
        Assert.Contains("Loading…", Section(Build(loading), "Remote links"));
        Assert.Contains("Attachments 1", Text(Build(failed)));
    }

    [Fact]
    public void GivenRemoteLinks_WhenBuilding_ThenTitleApplicationAndUrlShowAndControlCharactersAreDropped()
    {
        var detail = Detail(Issue()) with
        {
            RemoteLinks =
            [
                new RemoteLink("1", "https://example.com/wiki/page", "Design spec", "Confluence"),
                new RemoteLink("2", $"https://example.com/{_control}[31mred", "Plain\u0007 page", null),
            ],
        };

        var text = Text(Build(detail));

        Assert.Contains("Design spec · Confluence", text);
        Assert.Contains("https://example.com/wiki/page", text);
        Assert.Contains("Plain page", text);
        Assert.DoesNotContain(_control, text);
        Assert.DoesNotContain('\u0007', text);
    }

    [Fact]
    public void GivenAttachments_WhenBuilding_ThenNameTypeAndHumanSizeShowAndNothingIsRendered()
    {
        var issue = Issue() with
        {
            Attachments =
            [
                new IssueAttachment("1", "notes.txt", "text/plain", 47, _now.AddDays(-2), null, "https://x/content/1"),
                new IssueAttachment("2", "photo.png", "image/png", 3_500_000, null, null, null),
                new IssueAttachment("3", "mystery", string.Empty, 2048, null, null, null),
            ],
        };

        var text = Text(Build(Detail(issue)));

        Assert.Contains("Attachments 3", text);
        Assert.Contains("notes.txt · text/plain · 47 B · 2 d ago", text);
        Assert.Contains("photo.png · image/png · 3.3 MiB", text);
        Assert.Contains("mystery · unknown type · 2.0 KiB", text);
        Assert.DoesNotContain("https://x/content/1", text);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    [InlineData(20)]
    public void GivenWideCharactersAndLongNames_WhenBuilding_ThenNoLineIsWiderThanTheWidth(int width)
    {
        var issue = Epic() with
        {
            Attachments =
            [
                new IssueAttachment("1", "日本語の長いファイル名のノートです.txt", "text/plain", 47, _now, null, null),
                new IssueAttachment(
                    "2",
                    "émoji-😀-name-that-is-really-long-and-keeps-going.png",
                    "image/png",
                    9,
                    _now,
                    null,
                    null),
            ],
            Subtasks = [new IssueStub("A-18", "日本語のとても長いサブタスクの題名がここに入ります", _todo, _subtask)],
        };
        var detail = Detail(issue) with
        {
            Children = [Row("A-21", "Child 😀 with a long title that needs to wrap around")],
            RemoteLinks = [new RemoteLink("1", $"https://example.com/{new string('a', 200)}", "日本語", "Confluence")],
        };

        var document = Build(detail, width);

        Assert.All(
            document.Lines,
            line => Assert.True(DisplayWidth.Of(line.PlainText) <= width, $"{width}: {line.PlainText}"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    public void GivenTinyWidth_WhenBuildingEveryRelation_ThenItNeverThrowsAndStillHasContent(int width)
    {
        var detail = Detail(Epic()) with { Children = [Row("A-21", "Child")] };

        var document = Build(detail, width);

        Assert.True(document.Lines.Count > 10);
    }

    private static DetailDocument Build(DetailState detail, int width = 80)
    {
        return IssueDocumentBuilder.Build(detail, width);
    }

    private static string Text(DetailDocument document)
    {
        return string.Join('\n', document.Lines.Select(static line => line.PlainText));
    }

    private static List<string> Panels(DetailDocument document)
    {
        return [.. document.PaneTitles.Select(static title => string.Concat(title.Select(static span => span.Text)))];
    }

    /// <summary>The lines of the panel with the given title prefix, its shared top edge included.</summary>
    private static string Section(DetailDocument document, string title)
    {
        var index = Panels(document).FindIndex(panel => panel.StartsWith(title));
        var start = Math.Max(0, document.Sections[index] - 1);
        var end = index + 1 < document.Sections.Count ? document.Sections[index + 1] - 1 : document.Lines.Count;

        return string.Join('\n', document.Lines.Skip(start).Take(end - start).Select(static line => line.PlainText));
    }

    private static DetailState Detail(IssueDetail issue)
    {
        return DetailState.Opened("A-1", null, 0, 0, 0) with
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

    private static IssueSummary Row(string key, string summary)
    {
        return new IssueSummary("1", key, summary, _todo, _story, null, null, [], null, null, "A");
    }

    private static IssueDetail Epic()
    {
        return Issue() with
        {
            IssueType = _epic,
            Subtasks = [new IssueStub("A-18", "Write the migration", _todo, _subtask)],
            Links =
            [
                new IssueLink(
                    "1",
                    "Blocks",
                    IssueLinkDirection.Outward,
                    "blocks",
                    new IssueStub("A-30", "Launch", _todo, _story)),
                new IssueLink(
                    "2",
                    "Blocks",
                    IssueLinkDirection.Inward,
                    "is blocked by",
                    new IssueStub("A-9", "Legal review", _done, _story)),
            ],
            Attachments = [new IssueAttachment("1", "notes.txt", "text/plain", 47, _now.AddDays(-2), null, null)],
        };
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "A summary",
            _todo,
            _story,
            null,
            null,
            null,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);
    }
}
