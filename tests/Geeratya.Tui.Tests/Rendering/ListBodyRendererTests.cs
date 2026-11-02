using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ListBodyRendererTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Fact]
    public void GivenNoTab_WhenBuilding_ThenTheStartScreenStillShows()
    {
        var lines = Body(AppState.Initial, 60, 10);

        Assert.Contains(lines, line => line.Contains("Geeratya"));
    }

    [Fact]
    public void GivenLoadedRows_WhenBuilding_ThenHeaderTitlesAndRowsFollowTheSpec()
    {
        var lines = Frame(Tab(LoadState.Idle, 3) with { Selected = 1 }, 120);

        Assert.StartsWith("project = A · 3 issues", lines[0]);
        Assert.Matches(@"^  Key +T Summary +Status +Assignee", lines[1]);
        Assert.Matches(@"^  A-1 +T Summary +To Do +-", lines[2]);
        Assert.Matches(@"^> A-2 +T Summary +To Do +-", lines[3]);
        Assert.StartsWith("  A-3", lines[4]);
    }

    [Fact]
    public void GivenOneRow_WhenBuilding_ThenTheCountIsSingular()
    {
        Assert.StartsWith("project = A · 1 issue ", Frame(Tab(LoadState.Idle, 1))[0]);
    }

    [Fact]
    public void GivenMoreRowsToLoad_WhenBuilding_ThenTheCountHasAPlus()
    {
        var tab = Tab(LoadState.Idle, 3) with { Next = new PageCursor("n2") };

        Assert.StartsWith("project = A · 3+", Frame(tab)[0]);
    }

    [Fact]
    public void GivenSelection_WhenBuilding_ThenTheMarkerAndTheSelectedRoleMarkOnlyThatRow()
    {
        var body = Render(Tab(LoadState.Idle, 3) with { Selected = 2 }, 80, 10);

        Assert.StartsWith("  A-1", body[2].PlainText);
        Assert.StartsWith("> A-3", body[4].PlainText);
        Assert.Equal(TextRole.Selected, Assert.Single(body[4].Spans).Role);
        Assert.Equal(80, TextWidth.Of(body[4].PlainText));
        Assert.DoesNotContain(body[2].Spans, span => span.Role == TextRole.Selected);
    }

    [Fact]
    public void GivenStatusCategories_WhenBuilding_ThenTheStatusUsesTheCategoryRole()
    {
        var rows = new[]
        {
            Issue(1, status: new IssueStatus("1", "To Do", "new")),
            Issue(2, status: new IssueStatus("2", "Doing", "indeterminate")),
            Issue(3, status: new IssueStatus("3", "Done", "done")),
            Issue(4, status: new IssueStatus("4", "Odd", "other")),
        };

        var body = Render(Tab(LoadState.Idle) with { Rows = rows, Selected = 3 }, 120, 10);

        Assert.Contains(body[2].Spans, span => span.Role == TextRole.StatusToDo && span.Text.StartsWith("To Do"));
        Assert.Contains(body[3].Spans, span => span.Role == TextRole.StatusInProgress);
        Assert.Contains(body[4].Spans, span => span.Role == TextRole.StatusDone);
    }

    [Fact]
    public void GivenNarrowWidths_WhenBuilding_ThenAssigneeThenStatusNameDrop()
    {
        var tab = Tab(LoadState.Idle, 2) with { Rows = [Issue(1, assignee: "Jane Doe")] };

        Assert.Contains("Jane Doe", Frame(tab)[2]);
        Assert.DoesNotContain("Jane Doe", Frame(tab, 99)[2]);
        Assert.Contains("To Do", Frame(tab, 56)[2]);
        Assert.DoesNotContain("To Do", Frame(tab, 55)[2]);
        Assert.EndsWith("○", Frame(tab, 40)[2].TrimEnd());
    }

    [Fact]
    public void GivenALongKey_WhenBuilding_ThenTheKeyColumnIsCappedAtTwelve()
    {
        var longKey = Issue(1) with { Key = "ABCDEFGHIJ-123456" };
        var tab = Tab(LoadState.Idle) with { Rows = [longKey], KeyWidth = longKey.Key.Length };

        Assert.StartsWith("> ABCDEFGHIJ-1 T", Frame(tab, 80)[2]);
    }

    [Fact]
    public void GivenWideAndCombiningSummaries_WhenBuilding_ThenColumnsStayAligned()
    {
        var tab = Tab(LoadState.Idle) with
        {
            Rows = [Issue(1, "日本語日本語😀😀 combining é"), Issue(2, "plain")],
            KeyWidth = 3,
        };

        var lines = Frame(tab, 80);

        Assert.Equal(TextWidth.Of(lines[2]), TextWidth.Of(lines[3]));
        Assert.Equal(
            TextWidth.Of(lines[2][..lines[2].IndexOf("To Do", StringComparison.Ordinal)]),
            TextWidth.Of(lines[3][..lines[3].IndexOf("To Do", StringComparison.Ordinal)]));
    }

    [Fact]
    public void GivenTenThousandRows_WhenRendering_ThenOnlyTheViewportRowsAreProduced()
    {
        var rows = Enumerable.Range(1, 10_000).Select(number => Issue(number)).ToArray();
        var tab = Tab(LoadState.Idle) with { Rows = rows, Selected = 5_000, Scroll = 4_995, KeyWidth = 7 };

        var body = Render(tab, 120, 20);

        Assert.Equal(20, body.Count);
        Assert.Contains("A-4996", body[2].PlainText);
        Assert.Contains("A-5013", body[19].PlainText);
    }

    [Fact]
    public void GivenLoadingFirstPage_WhenBuilding_ThenLoadingShowsUnderTheHeader()
    {
        var lines = Frame(Tab(new Loading(LoadReason.Open)));

        Assert.StartsWith("project = A · 0 issues", lines[0]);
        Assert.StartsWith("Loading…", lines[2]);
    }

    [Fact]
    public void GivenLoadingMoreWithRoomBelow_WhenBuilding_ThenTheLoadingMoreRowFollowsTheRows()
    {
        var lines = Frame(Tab(new Loading(LoadReason.NextPage), 3));

        Assert.StartsWith("Loading more…", lines[5]);
        Assert.DoesNotContain("Loading more", lines[0]);
    }

    [Fact]
    public void GivenLoadingMoreWithRowsFillingTheScreen_WhenBuilding_ThenTheHeaderCarriesTheNote()
    {
        var lines = Body(Active(Tab(new Loading(LoadReason.NextPage), 20)), 60, 8);

        Assert.Contains("Loading more…", lines[0]);
    }

    [Fact]
    public void GivenAFailedNextPage_WhenBuilding_ThenTheRowsStayAndTheFooterNamesTheReason()
    {
        var lines = Frame(Tab(new Failed(JiraErrorKind.RateLimited, "Jira is rate limiting requests.", LoadReason.NextPage), 2));

        Assert.Contains("A-1", lines[2]);
        Assert.StartsWith("Could not load more: Jira is rate limiting requests.", lines[4]);
    }

    [Fact]
    public void GivenFailedFirstLoad_WhenBuilding_ThenTheErrorAndTheRetryHintShow()
    {
        var lines = Frame(Tab(new Failed(JiraErrorKind.Unauthorized, "Jira rejected the credentials.")));

        Assert.StartsWith("Jira rejected the credentials.", lines[2]);
        Assert.StartsWith("<space>r to retry", lines[3]);
    }

    [Fact]
    public void GivenEmptyJqlTab_WhenBuilding_ThenTheMessageIsCentredAndTheQueryShown()
    {
        var lines = Frame(Tab(LoadState.Idle), 60);

        Assert.Equal("No issues match.", lines[2].Trim());
        Assert.StartsWith(" ", lines[2]);
        Assert.StartsWith("project = A", lines[3]);
    }

    [Fact]
    public void GivenEmptyBoardTab_WhenBuilding_ThenTheBoardMessageShows()
    {
        var tab = Tab(LoadState.Idle);
        var board = tab with { Definition = tab.Definition with { Source = ViewSourceKind.Board } };

        Assert.Equal("The board has no issues.", Frame(board, 60)[2].Trim());
    }

    [Fact]
    public void GivenTruncatedTab_WhenBuilding_ThenTheCapIsNamed()
    {
        var lines = Frame(Tab(LoadState.Idle, 2) with { Truncated = true });

        Assert.StartsWith("Load more stopped at 10 000 rows.", lines[4]);
    }

    [Fact]
    public void GivenColumnsLayout_WhenBuilding_ThenTheListRendererStepsAside()
    {
        var tab = Tab(LoadState.Idle, 2);
        var columns = tab with { Definition = tab.Definition with { Layout = ViewLayout.Columns } };

        Assert.False(new ListBodyRenderer().AppliesTo(Active(columns)));
    }

    [Fact]
    public void GivenWideSummaryAndTinySizes_WhenBuilding_ThenEveryRowKeepsTheWidth()
    {
        var tab = Tab(LoadState.Idle) with { Rows = [Issue(1, "日本語日本語😀😀 combining é")], KeyWidth = 4 };
        var states = new[]
        {
            Active(tab),
            Active(tab with { Load = new Loading(LoadReason.NextPage) }),
            Active(tab with { Rows = [] }),
        };

        foreach (var state in states)
        {
            for (var width = 1; width <= 130; width++)
            {
                for (var height = 1; height <= 8; height++)
                {
                    var lines = Body(state, width, height);

                    Assert.Equal(height, lines.Count);
                    Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
                }
            }
        }
    }

    [Theory]
    [InlineData("new", ".")]
    [InlineData("indeterminate", "~")]
    [InlineData("done", "x")]
    [InlineData("other", "?")]
    public void GivenTheAsciiGlyphSet_WhenRenderingNarrowRows_ThenStatusUsesAsciiGlyphs(string category, string glyph)
    {
        var tab = Tab(LoadState.Idle) with { Rows = [Issue(1, status: new IssueStatus("1", "Any", category))] };

        var lines = Render(tab, 40, 6, GlyphSet.Ascii);

        Assert.EndsWith($" {glyph}", lines[2].PlainText.TrimEnd());
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenRenderingEveryListState_ThenNoNonAsciiCharacterIsDrawn()
    {
        var failed = new Failed(JiraErrorKind.RateLimited, "Jira is rate limiting requests.", LoadReason.NextPage);
        var states = new[]
        {
            Tab(LoadState.Idle, 3) with { Next = new PageCursor("n") },
            Tab(new Loading(LoadReason.NextPage), 3),
            Tab(LoadState.Idle, 3) with { Truncated = true },
            Tab(failed, 3),
            Tab(new Loading(LoadReason.Open)),
            Tab(LoadState.Idle) with { Rows = [Issue(1, status: new IssueStatus("2", "Doing", "indeterminate"))] },
            Tab(LoadState.Idle) with { Rows = [Issue(1, status: new IssueStatus("3", "Done", "done"))] },
        };

        foreach (var tab in states)
        {
            foreach (var width in new[] { 40, 80, 120 })
            {
                var lines = Render(tab, width, 10, GlyphSet.Ascii).Select(line => line.PlainText);

                Assert.All(lines, line => Assert.True(line.All(character => character < 128), line));
            }
        }
    }

    [Fact]
    public void GivenTheUnicodeGlyphSet_WhenRenderingNarrowRows_ThenTheCategoryCirclesAreDrawn()
    {
        var tab = Tab(LoadState.Idle, 1);

        var lines = Render(tab, 40, 6);

        Assert.Contains('○', lines[2].PlainText);
    }

    private static IReadOnlyList<StyledLine> Render(TabState tab, int width, int height, GlyphSet glyphs = GlyphSet.Unicode)
    {
        var context = new BodyContext(_helpLines, _keymap, glyphs);

        return new ListBodyRenderer().Render(Active(tab), context, width, height);
    }

    private static List<string> Frame(TabState tab, int width = 100)
    {
        return Body(Active(tab), width, 10);
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static TabState Tab(LoadState load, int rows = 0)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, 10);

        return opened with
        {
            Load = load,
            Rows = [.. Enumerable.Range(1, rows).Select(number => Issue(number))],
            KeyWidth = 3,
        };
    }

    private static IssueSummary Issue(int number, string summary = "Summary", IssueStatus? status = null, string? assignee = null)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            summary,
            status ?? new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            assignee is null ? null : new IssueAssignee("acc", assignee),
            [],
            null,
            null,
            "A");
    }

    /// <summary>The frame without its header lines, so the rows below them keep their meaning.</summary>
    private static List<string> Body(AppState state, int width, int height)
    {
        return [.. FrameBuilder.Build(state, width, height + FrameBuilder.HeaderRows, _helpLines, _keymap)
            .Skip(FrameBuilder.HeaderRows)];
    }
}
