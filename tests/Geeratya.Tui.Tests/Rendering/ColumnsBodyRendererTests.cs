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
using Geeratya.Tui.Rendering.Themes;

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
    public void GivenAMarkedCard_WhenRendering_ThenOnlyThatCardShowsAStarBeforeItsKeyInBothCardSizes()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2")) with { Marks = ["A-2"] };

        var full = Render(tab, 120, 30);
        var compact = Render(tab, 120, 8);

        Assert.Single(full, line => line.PlainText.Contains('*'));
        Assert.Contains("*A-2", full.Single(line => line.PlainText.Contains('*')).PlainText);
        Assert.Single(compact, line => line.PlainText.Contains("*A-2", StringComparison.Ordinal));
        Assert.DoesNotContain(compact, line => line.PlainText.Contains("*A-1", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFilterAndALane_WhenRendering_ThenTheTitleNamesBothAndOnlyTheLaneCardsAreDrawn()
    {
        var other = Issue("1", "A-2") with { Assignee = new IssueAssignee("other", "Bo Li") };
        var tab = Tab(Issue("1", "A-1", assignee: "Al Ek"), other) with
        {
            Swimlane = SwimlaneKind.Assignee,
            Lane = "other",
            QuickFilters = [new QuickFilter(5, "Recently Updated", "updatedDate >= -1d", 0)],
            ActiveQuickFilters = [5],
        };

        var body = Render(tab, 120, 30);
        var title = ColumnsBodyRenderer.StatusText(tab, Glyphs.For(GlyphSet.Unicode), default);

        Assert.Contains("Filter: Recently Updated", title);
        Assert.Contains("Lane 2/2: Bo Li", title);
        Assert.Contains(body, line => line.PlainText.Contains("A-2", StringComparison.Ordinal));
        Assert.DoesNotContain(body, line => line.PlainText.Contains("A-1", StringComparison.Ordinal));
    }

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
        Assert.StartsWith("╭─ To Do 2 ", body[1].PlainText);
        Assert.Contains("┬─ Doing 1 ", body[1].PlainText);
        Assert.Contains("┬─ Empty 0 ", body[1].PlainText);
        Assert.Contains("┬─ Done 0 ", body[1].PlainText);
        Assert.EndsWith("╮", body[1].PlainText);
        Assert.StartsWith("╰", body[^1].PlainText);
        Assert.EndsWith("╯", body[^1].PlainText);
        Assert.Contains("┴", body[^1].PlainText);
        var toDo = body[1].Spans.Single(span => span.Text.Contains("To Do", StringComparison.Ordinal));
        Assert.Equal(TextRole.ChipFocus, toDo.Role);
        Assert.DoesNotContain(
            body[1].Spans.Where(span => span.Text.Contains("Doing", StringComparison.Ordinal)),
            span => span.Role == TextRole.ChipFocus);
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
        Assert.Contains("┃ A-2  Task", body[10].PlainText);
        Assert.Contains("┗━", body[11].PlainText);
        var top = body[7].Spans.First(span => span.Text.Contains('┏', StringComparison.Ordinal));
        var summary = body[8].Spans.First(span => span.Text.Contains("Summary", StringComparison.Ordinal));
        Assert.Equal(TextRole.FrameFocus, top.Role);
        Assert.Equal(SpanGround.Selection, summary.Ground);
        Assert.DoesNotContain(body[2].Spans, span => span.Ground == SpanGround.Selection);
        Assert.Single(body, line => line.PlainText.Contains('┏', StringComparison.Ordinal));
        Assert.All(body.Skip(1), line => Assert.Equal(120, TextWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenANarrowColumn_WhenRendering_ThenTheTypeNameShrinksAndThenFallsBackToTheMarker()
    {
        var tab = Tab(Issue("1", "ABC-12345"));

        var wide = Render(tab, 120, 30);
        var narrow = Render(tab, 20, 30);

        Assert.Contains("┃ ABC-12345  Task", wide[5].PlainText);
        Assert.DoesNotContain("Task", narrow[5].PlainText);
        Assert.Contains("ABC-12345  T", narrow[5].PlainText);
    }

    [Fact]
    public void GivenAnAssigneeAndANarrowColumn_WhenRendering_ThenTheInitialsChipGoesBeforeTheTypeWord()
    {
        var tab = Tab(Issue("1", "ABCDEFGH-12345", assignee: "Jane Doe"));

        var body = Render(tab, 200, 30);
        var tight = Render(tab, 20, 30);

        Assert.Contains("ABCDEFGH-12345  Task", body[5].PlainText);
        Assert.Contains("[JD]", body[5].PlainText);
        Assert.DoesNotContain("[JD]", tight[5].PlainText);
    }

    [Fact]
    public void GivenALabelledCard_WhenRendering_ThenItShowsOneSummaryLineThenTheChipsThenTheBottomRow()
    {
        var issue = Issue("1", "A-1", "A summary that is long enough to need a second line on a narrow card")
            with { Labels = ["alpha", "beta"], Priority = "High" };

        var body = Render(Tab(issue), 120, 30);

        Assert.Contains("A summary", body[3].PlainText);
        Assert.Contains("enough to need", body[4].PlainText);
        Assert.Contains(" alpha   beta ", body[6].PlainText);
        Assert.Contains("A-1  Task", body[8].PlainText);
        Assert.Contains("▲ [--]", body[8].PlainText);
        Assert.Contains(body[6].Spans, span => span.Role == LabelChips.For("alpha") && span.Text == " alpha ");
        Assert.Contains(body[8].Spans, span => span.Role == TextRole.UnassignedChip);
        Assert.Contains(body[8].Spans, span => span.Role == TextRole.PriorityHigh && span.Text == "▲");
        Assert.Equal(string.Empty, body[5].PlainText[2..^2].Trim('│', '┃', ' '));
        Assert.Equal(string.Empty, body[7].PlainText[2..^2].Trim('│', '┃', ' '));
        Assert.All(body.Skip(1), line => Assert.Equal(120, TextWidth.Of(line.PlainText)));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(24)]
    [InlineData(20)]
    [InlineData(12)]
    public void GivenALabelledPrioritisedCard_WhenRenderingWithAsciiGlyphs_ThenEveryLineKeepsItsWidthAndIsAscii(
        int width)
    {
        var issue = Issue("1", "A-1", "Summary", "Jane Doe") with
        {
            Labels = ["alpha", "a-very-long-label-name", "gamma"],
            Priority = "High",
        };
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P"), Labels = ["x"] };
        var tab = Tab(issue, child);

        var frame = FrameBuilder.Build(Active(tab), width, 30 + FrameBuilder.HeaderRows, _keymap, null, GlyphSet.Ascii);

        Assert.All(frame, line => Assert.Equal(width, TextWidth.Of(line)));
        Assert.All(frame, line => Assert.DoesNotContain(line, character => Glyphs.AsciiSwapped.Contains(character)));

        if (width >= 40)
        {
            Assert.Contains(frame, line => line.Contains("^ [JD]", StringComparison.Ordinal));
            Assert.Contains(frame, line => line.Contains(" alpha   a-very-long-label-name ", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GivenAnEmptyTypeName_WhenRendering_ThenTheMarkerShowsInsteadOfABlank()
    {
        var issue = Issue("1", "A-1") with { IssueType = new IssueTypeSummary("10", "", 0, false) };

        var body = Render(Tab(issue), 120, 30);

        Assert.Contains("┃ A-1  ?", body[5].PlainText);
    }

    [Fact]
    public void GivenAnAssignee_WhenRendering_ThenInitialsSitAtTheColumnsRightEdgeAndUnassignedHasTheSameWidthDashChip()
    {
        var tab = Tab(Issue("1", "A-1", assignee: "Jane Doe"), Issue("1", "A-2"));

        var body = Render(tab, 120, 30);

        Assert.Matches(@"^┃ A-1  Task +\[JD\] ┃", body[5].PlainText[2..]);
        Assert.Matches(@"^│ A-2  Task +\[--\] │", body[10].PlainText[2..]);
        Assert.DoesNotContain("JD", body[10].PlainText);
    }

    [Fact]
    public void GivenALongSummary_WhenRendering_ThenItWrapsOverTwoLinesWithAnEllipsisOnTheSecond()
    {
        var summary = "Fix the login redirect after the token refresh when the user comes back "
            + "from the identity provider";

        var body = Render(Tab(Issue("1", "A-1", summary)), 120, 30);

        Assert.Contains("Fix the login", body[3].PlainText);
        Assert.Contains("…", body[4].PlainText);
        Assert.DoesNotContain("provider", body[4].PlainText);
        Assert.Contains("A-1  Task", body[6].PlainText);
        Assert.StartsWith("│ ┗", body[7].PlainText);
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
        Assert.Contains("A-9", with[5].PlainText);
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
        Assert.Contains("‹4", body[1].PlainText);
        Assert.Equal(80, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenNarrowTerminal_WhenRendering_ThenOneColumnShowsWithItsNeighboursNamedInTheHeader()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 40, 8);

        Assert.StartsWith("╭─‹ To Do 1  Doing 1  Empty 0 ›", body[1].PlainText);
        Assert.Equal(TextRole.ChipFocus, body[1].Spans.Single(span => span.Text == "Doing 1").Role);
        Assert.StartsWith("│ T A-2 Summary", body[2].PlainText);
        Assert.Equal(40, TextWidth.Of(body[1].PlainText));
    }

    [Fact]
    public void GivenShortBody_WhenRendering_ThenCardsCollapseToOneLine()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2"), Issue("1", "A-3"));

        var compact = Render(tab, 120, 8);
        var full = Render(tab, 120, 20);

        Assert.StartsWith("│ T A-1 Summary", compact[2].PlainText);
        Assert.StartsWith("│ T A-2 Summary", compact[3].PlainText);
        Assert.Contains("┏", full[2].PlainText);
        Assert.Contains("┃ A-1  Task", full[5].PlainText);
        Assert.Contains("A-2  Task", full[10].PlainText);
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

        // These cards have five rows, so five fit the 26 rows and the scrolloff shrinks to two cards: A-29 on the
        // first card row, the selected A-31 two cards under it.
        Assert.Contains("A-29", body[5].PlainText);
        Assert.Contains("A-31  Task", body[5 + 10].PlainText);
        Assert.Contains("┏", body[2 + 10].PlainText);
        Assert.DoesNotContain(body, line => line.PlainText.Contains("A-3 ", StringComparison.Ordinal));
        Assert.Equal(30, body.Count);
    }

    [Fact]
    public void GivenMoreCardsToLoad_WhenRendering_ThenCountsGetAPlusAndTheTitleSaysLoading()
    {
        var tab = Tab(Issue("1", "A-1")) with { Next = new PageCursor("n"), Load = new Loading(LoadReason.NextPage) };

        var body = Render(tab, 120, 30);

        Assert.StartsWith("Board · 1+ · Loading more…", body[0].PlainText);
        Assert.StartsWith("╭─ To Do 1+ ", body[1].PlainText);
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

        Assert.StartsWith("╭─ Issues 1 ", body[1].PlainText);
        Assert.Contains("A-1", body[5].PlainText);
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
        Assert.All(body.Skip(1), line => Assert.True(TextWidth.Of(line.PlainText) <= width));
    }

    [Fact]
    public void GivenALongGoalWithWideCharacters_WhenRendering_ThenItIsCutToTheWidth()
    {
        var body = RenderSprint(SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true }, 90, 20);

        Assert.Contains("Ship the board", body[0].PlainText);
        Assert.All(body.Skip(1), line => Assert.Equal(90, TextWidth.Of(line.PlainText)));
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
    public void GivenAnEmptyScrumBoard_WhenRendering_ThenItSaysThereIsNoOpenSprintAndNamesTheBacklog()
    {
        var tab = Tab() with { Board = _configuration with { Type = BoardType.Scrum } };

        var body = Render(tab, 100, 20);

        Assert.Contains(body, line => line.PlainText.Contains("No open sprint on this board.", StringComparison.Ordinal));
        Assert.Contains(body, line => line.PlainText.Contains(":backlog", StringComparison.Ordinal));
        Assert.DoesNotContain(body, line => line.PlainText.Contains("The board has no issues.", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenNoActiveSprint_WhenRendering_ThenAClearEmptyStateOffersTheBacklog()
    {
        var tab = SprintTab() with { SprintResolved = true };

        var body = RenderSprint(tab, 100, 20);

        Assert.Contains(body, line => line.PlainText.StartsWith("│ No active sprint", StringComparison.Ordinal));
        Assert.Contains(body, line => line.PlainText.Contains("backlog", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenACardWithPoints_WhenRendering_ThenThePointsShowOnTheKeyLine()
    {
        var body = RenderSprint(SprintTab(Issue("1", "A-1") with { Estimate = 0.5 }), 120, 30);

        Assert.Contains(
            body,
            line => line.PlainText.Contains("A-1  Task", StringComparison.Ordinal)
                && line.PlainText.Contains("0.5p", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenASprintViewWithHiddenColumns_WhenRendering_ThenTheHeaderLineEndsWithTheHiddenMarker()
    {
        var tab = SprintTab(Issue("1", "A-1")) with { Sprint = _sprint, SprintResolved = true };

        var body = RenderSprint(tab, 40, 20);

        Assert.Matches("[‹›]", body[1].PlainText);
        Assert.Equal(40, TextWidth.Of(body[1].PlainText));
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
        return Titled(Active(tab), new BodyContext(_keymap, GlyphSet.Unicode, _now), width, height);
    }

    // The title line moved from the body to the status bar: tests keep reading it as the first line, above the frame
    // that now has the rest of the height.
    private static IReadOnlyList<StyledLine> Titled(AppState state, BodyContext context, int width, int height)
    {
        var tab = state.Session.Tabs.First(candidate => candidate.Id == state.Session.Active);
        var title = ColumnsBodyRenderer.StatusText(tab, Glyphs.For(context.Glyphs), context.Now);
        var frame = new ColumnsBodyRenderer().Render(state, context, width, height - 1);

        return [StyledLine.Of(title, TextRole.Heading), .. frame];
    }

    [Fact]
    public void GivenTheCursorInTheSecondColumn_WhenRendering_ThenOnlyThatBoxIsFocusedAndOthersAreQuiet()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2"), Issue("3", "A-3")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);
        var edge = body[1].Spans;

        Assert.Equal(TextRole.Border, edge[0].Role);
        Assert.Contains(edge, span => span.Role == TextRole.ChipFocus && span.Text.Contains("Doing"));
        Assert.DoesNotContain(edge, span => span.Role == TextRole.ChipFocus && span.Text.Contains("To Do"));
        Assert.Contains(edge, span => span.Role == TextRole.Muted && span.Text.Contains("To Do"));
        Assert.Contains(edge, span => span.Role == TextRole.FrameFocus && span.Text.Contains('┬'));
        Assert.Equal(TextRole.Border, body[^1].Spans[0].Role);
        Assert.Contains(body[^1].Spans, span => span.Role == TextRole.FrameFocus);
        Assert.Equal(TextRole.Border, body[^1].Spans[^1].Role);
    }

    [Fact]
    public void GivenAColumnWithoutFocus_WhenRendering_ThenItsRememberedCardHasNoCursorMark()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.DoesNotContain(body, line => line.Spans.Any(span => span.Text == ">"));
        Assert.Contains(body, line => line.PlainText.Contains("A-1  Task", StringComparison.Ordinal));
        Assert.DoesNotContain(body, line => HasSpan(line, TextRole.Selected, "A-1"));
    }

    [Fact]
    public void GivenACompactColumnWithoutFocus_WhenRendering_ThenOnlyTheCursorCardIsTintedAndNoMarkIsDrawn()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 8);

        Assert.DoesNotContain(body.SelectMany(line => line.Spans), span => span.Text == ">");
        Assert.Contains(body.SelectMany(line => line.Spans), span => span.Ground == SpanGround.Selection);
    }

    [Fact]
    public void GivenTheCursorCard_WhenRendering_ThenOnlyItsTextIsTintedAndKeepsTheTypeColour()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);
        var tinted = body.SelectMany(line => line.Spans).Where(span => span.Ground == SpanGround.Selection).ToList();

        Assert.Contains(tinted, span => span.Role == TextRole.TypeTask && span.Text == "Task");
        Assert.All(tinted, span => Assert.NotEqual(TextRole.Selected, span.Role));
        Assert.All(
            body.Where(line => !line.PlainText.Contains("A-2", StringComparison.Ordinal)
                && !line.PlainText.Contains("Summary", StringComparison.Ordinal)),
            line => Assert.DoesNotContain(line.Spans.Skip(1).Take(5), span => span.Ground == SpanGround.Selection));
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    public void GivenTheCursorCard_WhenResolvingColours_ThenTheTypeNameKeepsItsColourOnTheTint(ColorSupport tier)
    {
        var theme = BuiltInThemes.Find(BuiltInThemes.DefaultDarkName)!.Theme!;
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var span = Render(tab, 120, 30).SelectMany(line => line.Spans)
            .First(candidate => candidate.Role == TextRole.TypeTask && candidate.Ground == SpanGround.Selection);
        var style = Palette.Resolve(theme, tier, PaletteTable.For(span.Role), span.Ground, true);
        var plain = Palette.Resolve(theme, tier, PaletteTable.For(TextRole.Selected), SpanGround.Page, true);
        var own = Palette.Resolve(theme, tier, PaletteTable.For(span.Role), SpanGround.Page, true);

        Assert.NotEqual(plain.Foreground, style.Foreground);
        Assert.Equal(own.Foreground, style.Foreground);
    }

    [Fact]
    public void GivenACompactBoard_WhenRendering_ThenOnlyTheCursorCardIsTintedWithItsTypeColour()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("2", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 8);
        var line = body.Single(row => row.Spans.Any(span => span.Ground == SpanGround.Selection));

        Assert.Contains("A-1", line.PlainText, StringComparison.Ordinal);
        Assert.Contains("A-2", line.PlainText, StringComparison.Ordinal);
        Assert.Contains(line.Spans, span => span.Role == TextRole.TypeTask && span.Ground == SpanGround.Selection);
        Assert.Contains(line.Spans, span => span.Role == TextRole.TypeTask && span.Ground == SpanGround.Page);
    }

    private static bool HasSpan(StyledLine line, TextRole role, string text)
    {
        return line.Spans.Any(span => span.Role == role && span.Text.Contains(text, StringComparison.Ordinal));
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
        return Titled(state, new BodyContext(_keymap), width, height);
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
    public void GivenATaskCard_WhenRendering_ThenOnlyItsTypeNameCarriesTheTypeRoleAndTheSelectedCardKeepsItOnTheTint()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "A-2")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        var plain = body[5].Spans.Single(span => span.Role == TextRole.TypeTask);
        Assert.Equal("Task", plain.Text);
        Assert.Equal(SpanGround.Page, plain.Ground);
        Assert.Contains(body[10].Spans, span => span.Role == TextRole.TypeTask && span.Ground == SpanGround.Selection);
        Assert.Equal(120, TextWidth.Of(body[5].PlainText));
    }

    [Fact]
    public void GivenAnAssigneeAndANarrowColumn_WhenRendering_ThenTheKeyLineKeepsItsWidthAndTheMarkerIsColoured()
    {
        var tab = Tab(Issue("1", "A-1"), Issue("1", "ABCDEFGH-12345", assignee: "Jane Doe"));

        var body = Render(tab, 20, 30);

        var line = body.First(l => l.Spans.Any(span => span.Role == TextRole.TypeTask));
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
        Assert.Contains("│ └", body[10].PlainText);
        Assert.DoesNotContain("↑", string.Concat(body.Select(line => line.PlainText)));
        Assert.Contains("│ │   A-2  Task", body[9].PlainText);
        Assert.Contains("┃", body[3].PlainText);
    }

    [Fact]
    public void GivenAGroupWithTheLastChildSelected_WhenRendering_ThenItIsClosedByAHeavyBottomAndTheParentSideStaysLight()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(child, Issue("1", "A-1")) with { SelectedKey = "A-2" };

        var body = Render(tab, 120, 30);

        Assert.Contains("│ ┣━", body[6].PlainText);
        Assert.Contains("│ ┃   A-2  Task", body[9].PlainText);
        Assert.Contains("│ ┗━", body[10].PlainText);
        Assert.Contains("┛", body[10].PlainText);
        Assert.Contains("│ │ A-1  Task", body[5].PlainText);
    }

    [Fact]
    public void GivenAnUnselectedParent_WhenRendering_ThenItsSummaryIsStrongAndItsChildIsNot()
    {
        var child = Issue("1", "A-2") with { Parent = new IssueParent("A-1", "P") };
        var tab = Tab(child, Issue("1", "A-1"), Issue("1", "A-9")) with { SelectedKey = "A-9" };

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
        Assert.Contains("│ ┃   A-2  Task", body[9].PlainText);
        Assert.Contains("│ ┃", body[8].PlainText);
        Assert.Contains("│ ┣━", body[10].PlainText);
        Assert.Contains("│ │   A-3  Task", body[13].PlainText);
        Assert.Contains("│ └", body[14].PlainText);
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
