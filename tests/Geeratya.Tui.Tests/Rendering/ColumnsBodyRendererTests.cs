using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Jira.Transport;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ColumnsBodyRendererTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

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
    public void GivenCards_WhenRendering_ThenEachColumnIsFramedWithANameAndCountAndTheSelectedOneIsBold()
    {
        var body = Render(Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("2", "A-3")), 120, 30);

        Assert.StartsWith("Board · 3 issues", body[0].PlainText);
        Assert.StartsWith("┌─ To Do 2 ", body[1].PlainText);
        Assert.Contains("┬─ Doing 1 ", body[1].PlainText);
        Assert.Contains("┬─ Empty 0 ", body[1].PlainText);
        Assert.Contains("┬─ Done 0 ", body[1].PlainText);
        Assert.EndsWith("┐", body[1].PlainText);
        var toDo = body[1].Spans.Single(span => span.Text.Contains("To Do", StringComparison.Ordinal));
        Assert.Equal(TextRole.Heading, toDo.Role);
        Assert.DoesNotContain(
            body[1].Spans.Where(span => span.Text.Contains("Doing", StringComparison.Ordinal)),
            span => span.Role == TextRole.Heading);
        Assert.Equal(5, body[2].PlainText.Count(character => character == '│'));
    }

    [Fact]
    public void GivenARunningWriteOnACard_WhenRendering_ThenOnlyThatCardShowsTheBusyMarker()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"));
        var operation = new Operation(
            9,
            new RankRequest("A-2", "A-1", true, null),
            tab.Id,
            OperationPhase.Sending,
            [],
            DateTimeOffset.UnixEpoch);
        var state = Active(tab);
        var busy = state with { Session = state.Session with { Operations = [operation] } };

        var text = string.Join("\n", RenderState(busy, 120, 30).Select(line => line.PlainText));

        Assert.Equal(1, text.Count(character => character == '…'));
    }

    [Fact]
    public void GivenSelection_WhenRendering_ThenAHeavyBorderAndTheSelectedRoleCoverOnlyThatCard()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("2", "A-3")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.Contains("┌", body[2].PlainText);
        Assert.Contains("┏━", body[7].PlainText);
        Assert.Contains("┃ Task A-2", body[8].PlainText);
        Assert.Contains("┗━", body[11].PlainText);
        var top = body[7].Spans.First(span => span.Text.Contains('┏', StringComparison.Ordinal));
        var summary = body[9].Spans.First(span => span.Text.Contains("Summary", StringComparison.Ordinal));
        Assert.Equal(TextRole.Selected, top.Role);
        Assert.Equal(TextRole.Selected, summary.Role);
        Assert.DoesNotContain(body[2].Spans, span => span.Role == TextRole.Selected);
        Assert.Single(body, line => line.PlainText.Contains('┏', StringComparison.Ordinal));
        Assert.All(body.Skip(1), line => Assert.Equal(120, TextWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenANarrowColumn_WhenRendering_ThenTheTypeNameShrinksAndThenFallsBackToTheMarker()
    {
        var tab = Tab(Issue("1", "ABC-12345"));

        var wide = Render(tab, 120, 30);
        var narrow = Render(tab, 20, 30);

        Assert.Contains("┃ Task ABC-12345", wide[3].PlainText);
        Assert.DoesNotContain("Task", narrow[3].PlainText);
        Assert.Contains("T ABC-12345", narrow[3].PlainText);
    }

    [Fact]
    public void GivenAnAssigneeAndANarrowColumn_WhenRendering_ThenTheInitialsGoBeforeTheTypeName()
    {
        var tab = Tab(Issue("1", "ABCDEFGH-12345", assignee: "Jane Doe"));

        var body = Render(tab, 120, 30);
        var tight = Render(tab, 20, 30);

        Assert.Contains("Task ABCDEFGH-12345", body[3].PlainText);
        Assert.Contains("JD", body[3].PlainText);
        Assert.DoesNotContain("JD", tight[3].PlainText);
    }

    [Fact]
    public void GivenAnEmptyTypeName_WhenRendering_ThenTheMarkerShowsInsteadOfABlank()
    {
        var issue = Issue("1", "A-1") with { IssueType = new IssueTypeSummary("10", "", 0, false) };

        var body = Render(Tab(issue), 120, 30);

        Assert.Contains("┃ ? A-1", body[3].PlainText);
    }

    [Fact]
    public void GivenAnAssignee_WhenRendering_ThenInitialsSitAtTheColumnsRightEdgeAndUnassignedHasNone()
    {
        var tab = Tab(Issue("1", "A-1", assignee: "Jane Doe"), Issue("1", "A-2"));

        var body = Render(tab, 80, 30);

        Assert.Matches(@"^┃ Task A-1 +JD ┃", body[3].PlainText[2..]);
        Assert.Matches(@"^│ Task A-2 +  ", body[8].PlainText[2..]);
        Assert.DoesNotContain("JD", body[8].PlainText);
    }

    [Fact]
    public void GivenALongSummary_WhenRendering_ThenItWrapsOverTwoLinesWithAnEllipsisOnTheSecond()
    {
        var summary = "Fix the login redirect after the token refresh when the user comes back "
            + "from the identity provider";

        var body = Render(Tab(Issue("1", "A-1", summary)), 120, 30);

        Assert.Contains("Fix the login", body[4].PlainText);
        Assert.Contains("…", body[5].PlainText);
        Assert.DoesNotContain("provider", body[5].PlainText);
        Assert.StartsWith("│ ┗", body[6].PlainText);
    }

    [Fact]
    public void GivenEmptyColumn_WhenRendering_ThenItSaysNoIssuesInMutedText()
    {
        var body = Render(Tab(Issue("1", "A-1")), 120, 30);

        var span = body[2].Spans.First(candidate => candidate.Text.Contains("no issues", StringComparison.Ordinal));
        Assert.Equal(TextRole.Muted, span.Role);
    }

    [Fact]
    public void GivenAnIssueInNoColumn_WhenRendering_ThenAnUnmappedColumnComesLastOnlyThen()
    {
        var with = Render(Tab(Issue("1", "A-1"), Issue("99", "A-9")) with { SelectedKey = "A-9" }, 140, 30);
        var without = Render(Tab(Issue("1", "A-1")), 140, 30);

        Assert.Contains("Unmapped 1", with[1].PlainText);
        Assert.Contains("A-9", with[3].PlainText);
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

        Assert.Contains("Col6 1", body[1].PlainText);
        Assert.DoesNotContain("Col1 ", body[1].PlainText);
        Assert.EndsWith("‹4", body[0].PlainText.TrimEnd());
        Assert.Equal(80, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenNarrowTerminal_WhenRendering_ThenOneColumnShowsWithItsNeighboursNamedInTheHeader()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 40, 8);

        Assert.StartsWith("┌─‹ To Do 1  Doing 1  Empty 0 ›", body[1].PlainText);
        Assert.Equal(TextRole.Heading, body[1].Spans.Single(span => span.Text == "Doing 1").Role);
        Assert.StartsWith("│>T A-2 Summary", body[2].PlainText);
        Assert.Equal(40, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenShortBody_WhenRendering_ThenCardsCollapseToOneLine()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("1", "A-3"));

        var compact = Render(tab, 120, 8);
        var full = Render(tab, 120, 20);

        Assert.StartsWith("│>T A-1 Summary", compact[2].PlainText);
        Assert.StartsWith("│ T A-2 Summary", compact[3].PlainText);
        Assert.Contains("┏", full[2].PlainText);
        Assert.Contains("┃ Task A-1", full[3].PlainText);
        Assert.Contains("Task A-2", full[8].PlainText);
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

        // Five cards fit, so the scroll is corrected to 28: two cards of context above the selection (the most the
        // viewport allows while keeping scrolloff below it), A-29 on the first card row.
        Assert.Contains("A-29", body[3].PlainText);
        Assert.Contains("Task A-31", body[3 + (2 * 5)].PlainText);
        Assert.Contains("┏", body[2 + (2 * 5)].PlainText);
        Assert.DoesNotContain(body, line => line.PlainText.Contains("A-3 ", StringComparison.Ordinal));
        Assert.Equal(30, body.Count);
    }

    [Fact]
    public void GivenMoreCardsToLoad_WhenRendering_ThenCountsGetAPlusAndTheTitleSaysLoading()
    {
        var tab = Tab(Issue("1", "A-1")) with { Next = new PageCursor("n"), Load = new Loading(LoadReason.NextPage) };

        var body = Render(tab, 120, 30);

        Assert.StartsWith("Board · 1+ · Loading more…", body[0].PlainText);
        Assert.StartsWith("┌─ To Do 1+ ", body[1].PlainText);
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

        Assert.StartsWith("┌─ Issues 1 ", body[1].PlainText);
        Assert.Contains("A-1", body[3].PlainText);
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
                    var frame = FrameBuilder.Build(state, width, height + FrameBuilder.HeaderRows, _keymap);
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
        Assert.Equal(SpanCount(Render(Tab([.. rows.Take(200)]), 120, 36)), SpanCount(body));
    }

    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Sprint _sprint = new(
        7,
        "Seed Sprint 2",
        SprintState.Active,
        "Ship the board 日本語 and polish everything that is left on the list",
        new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
        null,
        4);

    [Theory]
    [InlineData(120, 30)]
    [InlineData(80, 20)]
    [InlineData(72, 8)]
    public void GivenASprintView_WhenRendering_ThenTheHeaderLineShowsSprintDatesDaysLeftCountAndPointsAtEverySize(
        int width,
        int height)
    {
        var rows = new[]
        {
            Issue("1", "A-1") with { Estimate = 5 },
            Issue("4", "A-2") with { Estimate = 3, Status = new IssueStatus("4", "Done", "done") },
        };

        var body = RenderSprint(SprintTab(rows) with { Sprint = _sprint, SprintResolved = true }, width, height);

        Assert.StartsWith("Seed Sprint 2 · 30 Oct - 13 Nov · 10d left · 2 issues · 3/8 pts", body[0].PlainText);
        Assert.Equal(height, body.Count);
        Assert.All(body, line => Assert.True(TextWidth.Of(line.PlainText) <= width));
    }

    [Fact]
    public void GivenALongGoalWithWideCharacters_WhenRendering_ThenItIsCutToTheWidth()
    {
        var body = RenderSprint(SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true }, 90, 20);

        Assert.Contains("Ship the board", body[0].PlainText);
        Assert.DoesNotContain("left on the list", body[0].PlainText);
        Assert.True(TextWidth.Of(body[0].PlainText) <= 90);
    }

    [Fact]
    public void GivenSprintWithoutGoalDatesOrPoints_WhenRendering_ThenOnlyNameAndCountShow()
    {
        var bare = new Sprint(7, "Bare", SprintState.Future, null, null, null, null, 4);

        var body = RenderSprint(SprintTab(Issue("1", "A-1")) with { Sprint = bare, SprintResolved = true }, 100, 20);

        Assert.Equal("Bare · 1 issue", body[0].PlainText.TrimEnd());
    }

    [Fact]
    public void GivenSprintViewWhoseSprintIsUnknown_WhenRendering_ThenTheTabTitleShows()
    {
        var body = RenderSprint(SprintTab(Issue("1", "A-1")), 100, 20);

        Assert.StartsWith("Board · 1 issue", body[0].PlainText);
    }

    [Fact]
    public void GivenNoActiveSprint_WhenRendering_ThenAClearEmptyStateOffersTheBacklog()
    {
        var tab = SprintTab() with { SprintResolved = true };

        var body = RenderSprint(tab, 100, 20);

        Assert.Contains(body, line => line.PlainText.StartsWith("No active sprint", StringComparison.Ordinal));
        Assert.Contains(body, line => line.PlainText.Contains("backlog", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenACardWithPoints_WhenRendering_ThenThePointsShowOnTheKeyLine()
    {
        var body = RenderSprint(SprintTab(Issue("1", "A-1") with { Estimate = 0.5 }), 120, 30);

        Assert.Contains(
            body,
            line => line.PlainText.Contains("Task A-1", StringComparison.Ordinal)
                && line.PlainText.Contains("0.5p", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenASprintViewWithHiddenColumns_WhenRendering_ThenTheHeaderLineEndsWithTheHiddenMarker()
    {
        var tab = SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true };

        var body = RenderSprint(tab, 40, 20);

        Assert.Matches("[‹›]", body[0].PlainText);
        Assert.True(TextWidth.Of(body[0].PlainText) <= 40);
    }

    [Fact]
    public void GivenASprintViewWithMoreRowsToLoad_WhenRendering_ThenThePartialCountIsMarked()
    {
        var tab = SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true, Truncated = true };

        var body = RenderSprint(tab, 140, 20);

        Assert.Contains("1+", body[0].PlainText);
    }

    [Fact]
    public void GivenSprintViewAndNoClock_WhenRendering_ThenNoDaysLeftShow()
    {
        var tab = SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true };
        var context = new BodyContext(_keymap);

        var body = new ColumnsBodyRenderer().Render(Active(tab), context, 120, 20);

        Assert.DoesNotContain("d left", body[0].PlainText);
        Assert.DoesNotContain("ago", body[0].PlainText);
    }

    private static IReadOnlyList<StyledLine> RenderSprint(TabState tab, int width, int height)
    {
        var context = new BodyContext(_keymap, GlyphSet.Unicode, _now);

        return new ColumnsBodyRenderer().Render(Active(tab), context, width, height);
    }

    private static TabState SprintTab(params IssueSummary[] rows)
    {
        var tab = Tab(rows);

        return tab with
        {
            Definition = tab.Definition with { Source = ViewSourceKind.Sprint, SourceValue = "4:active" },
        };
    }

    private static int SpanCount(IReadOnlyList<StyledLine> body)
    {
        return body.Sum(line => line.Spans.Count);
    }

    private static IReadOnlyList<StyledLine> Render(TabState tab, int width, int height)
    {
        return RenderState(Active(tab), width, height);
    }

    private static IReadOnlyList<StyledLine> RenderState(AppState state, int width, int height)
    {
        var context = new BodyContext(_keymap);

        return new ColumnsBodyRenderer().Render(state, context, width, height);
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

    [Fact]
    public void GivenATaskCard_WhenRendering_ThenOnlyItsTypeNameCarriesTheTypeRoleUnlessSelected()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        var plain = body[3].Spans.Single(span => span.Role == TextRole.TypeTask);
        Assert.Equal("Task", plain.Text);
        Assert.DoesNotContain(body[8].Spans, span => span.Role == TextRole.TypeTask);
        Assert.Equal(120, TextWidth.Of(body[3].PlainText));
    }

    [Fact]
    public void GivenAnAssigneeAndANarrowColumn_WhenRendering_ThenTheKeyLineKeepsItsWidthAndTheMarkerIsColoured()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "ABCDEFGH-12345", assignee: "Jane Doe"));

        var body = Render(tab, 20, 30);

        var line = body.Single(l => l.Spans.Any(span => span.Role == TextRole.TypeTask));
        Assert.Equal(20, TextWidth.Of(line.PlainText));
    }

    [Fact]
    public void GivenACompactCard_WhenRendering_ThenTheMarkerCarriesTheTypeRole()
    {
        var body = Render(Tab(Issue("1", "A-1"), Issue("1", "A-2")), 120, 8);

        Assert.Contains(body, line => line.Spans.Any(span => span is { Role: TextRole.TypeTask, Text: "T" }));
    }

    [Fact]
    public void GivenASubtaskUnderItsParent_WhenRendering_ThenBothShareOneOuterFrameWithIndentedChildAndHeavyParentBottom()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "Parent summary") };
        var tab = Tab(child, Issue("1", "A-1")) with { SelectedKey = "A-1" };

        var body = Render(tab, 120, 30);

        Assert.Contains("│ ┏━", body[2].PlainText);
        Assert.Contains("│ ┣━", body[6].PlainText);
        Assert.Contains("┫", body[6].PlainText);
        Assert.Contains("│ └", body[11].PlainText);
        Assert.DoesNotContain("↑", string.Concat(body.Select(line => line.PlainText)));
        Assert.Contains("│ │   Task A-2", body[7].PlainText);
        Assert.Contains("┃", body[3].PlainText);
    }

    [Fact]
    public void GivenAGroupWithTheLastChildSelected_WhenRendering_ThenItIsClosedByAHeavyBottomAndTheParentSideStaysLight()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(child, Issue("1", "A-1")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.Contains("│ ┣━", body[6].PlainText);
        Assert.Contains("│ ┃   Task A-2", body[7].PlainText);
        Assert.Contains("│ ┗━", body[11].PlainText);
        Assert.Contains("┛", body[11].PlainText);
        Assert.Contains("│ │ Task A-1", body[3].PlainText);
    }

    [Fact]
    public void GivenAnUnselectedParent_WhenRendering_ThenItsSummaryIsStrongAndItsChildIsNot()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(child, Issue("1", "A-1"), Issue("2", "A-9")) with { SelectedKey = "A-9" };

        var body = Render(tab, 120, 30);

        Assert.Contains(body[4].Spans, span => span.Role == TextRole.Strong);
        Assert.DoesNotContain(body[8].Spans, span => span.Role == TextRole.Strong);
    }

    [Fact]
    public void GivenAGroupOfThreeWithASelectedMiddleChild_WhenRendering_ThenOnlyThatChildHasHeavySidesAndTheFrameCloses()
    {
        var first = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var second = Issue("1", "A-3") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(Issue("1", "A-1"), first, second) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.Contains("│ ┣━", body[6].PlainText);
        Assert.Contains("│ ┃   Task A-2", body[7].PlainText);
        Assert.Contains("│ ┃", body[10].PlainText);
        Assert.Contains("│ ┣━", body[11].PlainText);
        Assert.Contains("│ │   Task A-3", body[12].PlainText);
        Assert.Contains("│ └", body[16].PlainText);
        Assert.DoesNotContain("┃", body[3].PlainText);
    }

    [Fact]
    public void GivenAParentWithChildrenWhoseOwnParentIsInAnotherColumn_WhenRendering_ThenTheFrameTopNamesIt()
    {
        var story = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "Epic summary") };
        var child = Issue("1", "A-3") with { Parent = new IssueParent("A-2", "S") };
        var tab = Tab(story, child, Issue("2", "A-1"));

        var body = Render(tab, 120, 30);

        Assert.Contains("━ ↑ A-1 Epic summary", body[2].PlainText);
    }

    [Fact]
    public void GivenASubtaskWhoseParentIsInAnotherColumn_WhenRendering_ThenTheTopBorderNamesTheParent()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "Parent summary") };
        var tab = Tab(child, Issue("2", "A-1"));

        var body = Render(tab, 120, 30);

        Assert.Contains("┏━ ↑ A-1 Parent summary ━", body[2].PlainText);
    }

    [Fact]
    public void GivenAnOrphanSubtask_WhenRenderingAtEveryWidth_ThenLinesKeepTheWidthAndCompactLinesNameTheParent()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "日本語 parent") };
        var tab = Tab(child);

        for (var width = 1; width <= 60; width++)
        {
            var frame = FrameBuilder.Build(Active(tab), width, 30 + FrameBuilder.HeaderRows, _keymap);

            Assert.All(frame, line => Assert.Equal(width, TextWidth.Of(line)));
        }

        Assert.Contains("↑A-1", Render(tab, 120, 8)[2].PlainText);
    }

    [Fact]
    public void GivenANestedCompactCard_WhenRendering_ThenItHasABranchGlyphWithTheTypeColourOnTheMarker()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(child, Issue("1", "A-1")) with { SelectedKey = "A-1" };

        var body = Render(tab, 120, 8);

        Assert.Contains(" └T A-2", body[3].PlainText);
        Assert.Contains(body[3].Spans, span => span.Text == "T" && span.Role == TextRole.TypeTask);
    }

    [Fact]
    public void GivenAGroupInNarrowColumns_WhenRenderingAtEveryWidth_ThenLinesKeepTheWidth()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "Parent") };
        var tab = Tab(child, Issue("1", "A-1")) with { SelectedKey = "A-2" };

        for (var width = 1; width <= 60; width++)
        {
            var frame = FrameBuilder.Build(Active(tab), width, 30 + FrameBuilder.HeaderRows, _keymap);

            Assert.All(frame, line => Assert.Equal(width, TextWidth.Of(line)));
        }
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
