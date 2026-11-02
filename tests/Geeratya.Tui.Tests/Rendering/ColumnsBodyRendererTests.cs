using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ColumnsBodyRendererTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [
            new BoardColumn("To Do", ["1"]),
            new BoardColumn("Doing", ["2"]),
            new BoardColumn("Empty", ["3"]),
            new BoardColumn("Done", ["4"]),
        ],
        "10001",
        null,
        null,
        null);

    [Fact]
    public void GivenColumnsLayout_WhenAskingWhoDraws_ThenOnlyTheColumnsRendererAppliesAndHelpWinsOverIt()
    {
        var tab = Tab(Issue("1", "A-1"));
        var list = tab with { Definition = tab.Definition with { Layout = ViewLayout.List } };

        Assert.True(new ColumnsBodyRenderer().AppliesTo(Active(tab)));
        Assert.False(new ColumnsBodyRenderer().AppliesTo(Active(list)));
        Assert.False(new ColumnsBodyRenderer().AppliesTo(Active(tab) with { Mode = InputMode.Help }));
        Assert.False(new ColumnsBodyRenderer().AppliesTo(AppState.Initial));
    }

    [Fact]
    public void GivenCards_WhenRendering_ThenEachColumnHasANameAndCountAndTheSelectedOneIsBracketed()
    {
        var body = Render(Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("2", "A-3")), 120, 30);

        Assert.StartsWith("Board · 3 issues", body[0].PlainText);
        Assert.StartsWith("[To Do 2]", body[1].PlainText);
        Assert.Contains(" Doing 1 ", body[1].PlainText);
        Assert.Contains(" Empty 0 ", body[1].PlainText);
        Assert.Contains(" Done 0 ", body[1].PlainText);
        Assert.Equal(TextRole.Heading, body[1].Spans[0].Role);
        Assert.All(body[1].Spans.Skip(1), span => Assert.Equal(TextRole.Muted, span.Role));
    }

    [Fact]
    public void GivenSelection_WhenRendering_ThenTheMarkAndTheSelectedRoleCoverOnlyThatCard()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("2", "A-3")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.StartsWith(" T A-1", body[2].PlainText);
        Assert.Contains(">T A-2", body[6].PlainText);
        var markSpan = body[6].Spans.First(span => span.Text.Contains('>', StringComparison.Ordinal));
        var summarySpan = body[7].Spans.First(span => span.Text.Contains("Summary", StringComparison.Ordinal));
        Assert.Equal(TextRole.Selected, markSpan.Role);
        Assert.Equal(TextRole.Selected, summarySpan.Role);
        Assert.DoesNotContain(body[2].Spans, span => span.Role == TextRole.Selected);
        Assert.Single(body, line => line.PlainText.Contains('>', StringComparison.Ordinal));
        Assert.All(body.Skip(1), line => Assert.Equal(120, TextWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenAnAssignee_WhenRendering_ThenInitialsSitAtTheColumnsRightEdgeAndUnassignedHasNone()
    {
        var tab = Tab(Issue("1", "A-1", assignee: "Jane Doe"), Issue("1", "A-2"));

        var body = Render(tab, 80, 30);

        Assert.Matches(@"^>T A-1 +JD ", body[2].PlainText);
        Assert.Matches(@"^ T A-2 +  ", body[6].PlainText);
        Assert.DoesNotContain("JD", body[6].PlainText);
    }

    [Fact]
    public void GivenALongSummary_WhenRendering_ThenItWrapsOverTwoLinesWithAnEllipsisOnTheSecond()
    {
        var summary = "Fix the login redirect after the token refresh when the user comes back "
            + "from the identity provider";

        var body = Render(Tab(Issue("1", "A-1", summary)), 80, 30);

        Assert.StartsWith(" Fix the login", body[3].PlainText);
        Assert.Contains("…", body[4].PlainText);
        Assert.DoesNotContain("provider", body[4].PlainText);
        Assert.Equal(string.Empty, body[5].PlainText.Trim());
    }

    [Fact]
    public void GivenEmptyColumn_WhenRendering_ThenItSaysNoIssuesInMutedText()
    {
        var body = Render(Tab(Issue("1", "A-1")), 120, 30);

        var span = body[2].Spans.First(candidate => candidate.Text.StartsWith("no issues", StringComparison.Ordinal));
        Assert.Equal(TextRole.Muted, span.Role);
    }

    [Fact]
    public void GivenAnIssueInNoColumn_WhenRendering_ThenAnUnmappedColumnComesLastOnlyThen()
    {
        var with = Render(Tab(Issue("1", "A-1"), Issue("99", "A-9")) with { SelectedKey = "A-9" }, 140, 30);
        var without = Render(Tab(Issue("1", "A-1")), 140, 30);

        Assert.Contains("[Unmapped 1]", with[1].PlainText);
        Assert.Contains("A-9", with[2].PlainText);
        Assert.DoesNotContain("Unmapped", without[1].PlainText);
    }

    [Fact]
    public void GivenMoreColumnsThanFit_WhenRendering_ThenTheWindowFollowsTheSelectionAndCountsHiddenColumns()
    {
        var statuses = Enumerable.Range(1, 8).Select(number => number.ToString(CultureInfo.InvariantCulture)).ToArray();
        var configuration = _configuration with
        {
            Columns = [.. statuses.Select(status => new BoardColumn($"Col{status}", [status]))],
        };
        var rows = statuses.Select(status => Issue(status, $"A-{status}")).ToArray();
        var tab = Tab(rows) with { Board = configuration, SelectedKey = "A-6" };

        var body = Render(tab, 80, 30);

        Assert.Contains("[Col6 1]", body[1].PlainText);
        Assert.DoesNotContain("Col1 ", body[1].PlainText);
        Assert.EndsWith("‹4", body[0].PlainText.TrimEnd());
        Assert.Equal(80, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenNarrowTerminal_WhenRendering_ThenOneColumnShowsWithItsNeighboursNamedInTheHeader()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 40, 8);

        Assert.StartsWith("‹ To Do 1  [Doing 1]  Empty 0 ›", body[1].PlainText);
        Assert.Equal(TextRole.Heading, body[1].Spans.Single(span => span.Text.StartsWith('[')).Role);
        Assert.StartsWith(">T A-2 Summary", body[2].PlainText);
        Assert.Equal(40, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenShortBody_WhenRendering_ThenCardsCollapseToOneLine()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("1", "A-3"));

        var compact = Render(tab, 120, 8);
        var full = Render(tab, 120, 12);

        Assert.StartsWith(">T A-1 Summary", compact[2].PlainText);
        Assert.StartsWith(" T A-2 Summary", compact[3].PlainText);
        Assert.StartsWith(">T A-1", full[2].PlainText);
        Assert.StartsWith(" T A-2", full[6].PlainText);
    }

    [Fact]
    public void GivenAColumnLongerThanTheViewport_WhenTheCursorIsFarDown_ThenTheScrolledCardsShow()
    {
        var rows = Enumerable.Range(1, 40).Select(number => Issue("1", $"A-{number}")).ToArray();
        var cards = new[] { 30, 0, 0, 0 };
        var tab = Tab(rows) with
        {
            SelectedKey = "A-31",
            Cursor = new BoardCursor(0, cards, [28, 0, 0, 0], 30),
        };

        var body = Render(tab, 120, 30);

        // The scroll is corrected to keep three cards of context above the selection.
        Assert.Contains(">T A-31", body[2 + ((30 - 27) * 4)].PlainText);
        Assert.Contains("A-28", body[2].PlainText);
        Assert.DoesNotContain(body, line => line.PlainText.Contains("A-3 ", StringComparison.Ordinal));
        Assert.Equal(30, body.Count);
    }

    [Fact]
    public void GivenMoreCardsToLoad_WhenRendering_ThenCountsGetAPlusAndTheTitleSaysLoading()
    {
        var tab = Tab(Issue("1", "A-1")) with { Next = new PageCursor("n"), Load = new Loading(LoadReason.NextPage) };

        var body = Render(tab, 120, 30);

        Assert.StartsWith("Board · 1+ · Loading more…", body[0].PlainText);
        Assert.StartsWith("[To Do 1+]", body[1].PlainText);
    }

    [Fact]
    public void GivenNoRows_WhenRenderingEachLoadState_ThenTheListMessagesShow()
    {
        var empty = Tab();

        var loading = Render(empty with { Load = new Loading(LoadReason.Open) }, 80, 10);
        var none = Render(empty, 80, 10);
        var failed = Render(empty with { Load = new Failed(JiraErrorKind.NotFound, "Board not found") }, 80, 10);

        Assert.Contains("Loading…", loading[2].PlainText);
        Assert.Contains("The board has no issues.", none[2].PlainText);
        Assert.Contains("Board not found", failed[2].PlainText);
        Assert.Contains("<space>r to retry", failed[3].PlainText);
    }

    [Fact]
    public void GivenRowsWithoutAConfiguration_WhenRendering_ThenTheyAreOneColumnRatherThanLost()
    {
        var tab = Tab(Issue("1", "A-1")) with { Board = null };

        var body = Render(tab, 80, 20);

        Assert.StartsWith("[Issues 1]", body[1].PlainText);
        Assert.Contains("A-1", body[2].PlainText);
    }

    [Fact]
    public void GivenWideAndCombiningText_WhenRenderingAtEverySmallSize_ThenEveryLineKeepsTheWidth()
    {
        var tab = Tab(
            Issue("1", "A-1", "日本語日本語😀😀 combining é plus a long tail of words", "日本 語"),
            Issue("2", "A-2"),
            Issue("99", "A-9"));
        var states = new[]
        {
            Active(tab),
            Active(tab with { Next = new PageCursor("n"), Load = new Loading(LoadReason.NextPage) }),
            Active(tab with { Rows = [] }),
            Active(tab with { Cursor = new BoardCursor(2, [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], 0) }),
        };

        foreach (var state in states)
        {
            for (var width = 1; width <= 130; width++)
            {
                for (var height = 1; height <= 14; height++)
                {
                    var frame = FrameBuilder.Build(state, width, height + FrameBuilder.HeaderRows, _helpLines, _keymap);
                    var lines = frame.Skip(FrameBuilder.HeaderRows).ToList();

                    Assert.Equal(height, lines.Count);
                    Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
                }
            }
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(2000)]
    public void GivenBigBoard_WhenRendering_ThenOnlyTheViewportIsBuilt(int count)
    {
        var rows = Enumerable.Range(1, count)
            .Select(number => Issue(((number % 4) + 1).ToString(CultureInfo.InvariantCulture), $"A-{number}"))
            .ToArray();
        var tab = Tab(rows);

        var body = Render(tab, 120, 36);

        Assert.Equal(36, body.Count);
        Assert.True(body.Sum(line => line.Spans.Count) < 36 * 10);
    }

    private static IReadOnlyList<StyledLine> Render(TabState tab, int width, int height)
    {
        var context = new BodyContext(_helpLines, _keymap);

        return new ColumnsBodyRenderer().Render(Active(tab), context, width, height);
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static TabState Tab(params IssueSummary[] rows)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, 10);

        return opened with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = rows.Length == 0 ? null : rows[0].Key,
            KeyWidth = 3,
        };
    }

    private static IssueSummary Issue(string statusId, string key, string summary = "Summary", string? assignee = null)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus(statusId, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            assignee is null ? null : new IssueAssignee("acc", assignee),
            [],
            null,
            null,
            "A");
    }
}
