using System.Text.Json;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The issue detail at the three snapshot sizes with the longest recorded description and wide characters in the
/// comments. To refresh after an intended change, run the tests with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the
/// diff of the written files.
/// </summary>
public class IssueDetailRendererTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);
    private readonly AppReducer _reducer = new(_keymap);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenALoadedIssue_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        AssertSnapshot(Loaded(width, height), width, height, $"detail-{width}x{height}.txt");
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnEpicWithEveryRelation_WhenScrolledToTheEnd_ThenTheRelationPanelsMatchTheSnapshot(
        int width,
        int height)
    {
        var state = _reducer.Reduce(LoadedEpic(width, height), new KeyPressed("G")).State;

        AssertSnapshot(state, width, height, $"detail-relations-{width}x{height}.txt");
    }

    [Fact]
    public void GivenALoadedIssueScrolledToTheEnd_WhenBuildingTheFrame_ThenItShowsTheCommentsAndMatchesTheSnapshot()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("G")).State;

        AssertSnapshot(state, 80, 24, "detail-bottom-80x24.txt");
    }

    [Fact]
    public void GivenCursorMoves_WhenBuilding_ThenOnlyTheFocusedFieldRowCarriesTheMarkerAndEdgesAreSkipped()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("j")).State;

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        var marked = lines.Where(line => line.StartsWith("│>")).ToList();
        Assert.Single(marked);
        Assert.StartsWith("│>Status", lines[FrameBuilder.HeaderRows + 3]);
        Assert.DoesNotContain(lines, line => line.StartsWith('>'));
        Assert.Contains(lines, line => line.StartsWith("│ Fix login"));
        Assert.Contains(lines, line => line.StartsWith("╭─ GCM-12"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(11)]
    public void GivenFramesDroppedOrTiny_WhenTheCursorIsShown_ThenTheRowStaysWithinTheWidth(int width)
    {
        var lines = FrameBuilder.Build(Loaded(80, 24), width, 12, _keymap);

        Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenFocusOnAField_WhenRendering_ThenTheWholeRowIsSelectedAndOtherFieldsAreNot()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("j")).State;
        var context = new BodyContext(_keymap);

        var lines = new IssueDetailRenderer().Render(state, context, 80, 24);

        var status = lines.Single(line => line.PlainText.Contains("Status"));
        var priority = lines.Single(line => line.PlainText.Contains("Priority"));
        Assert.Contains(status.Spans, span => span.Role == TextRole.Selected && span.Text.Contains("Status"));
        Assert.Contains(status.Spans, span => span.Role == TextRole.Selected && span.Text.Contains('▾'));
        Assert.DoesNotContain(priority.Spans, span => span.Role == TextRole.Selected);
    }

    [Fact]
    public void GivenTheDescriptionFolded_WhenBuildingTheFrame_ThenItShowsAPreviewAndMatchesTheSnapshot()
    {
        var state = Keys(Loaded(80, 24), "]", "]", "z", "a");

        AssertSnapshot(state, 80, 24, "detail-folded-80x24.txt");
    }

    [Fact]
    public void GivenTheDescriptionExpanded_WhenBuildingTheFrame_ThenItShowsAllLinesAndMatchesTheSnapshot()
    {
        var state = Keys(Loaded(80, 24), "]", "]", "j", "j", "j", "j", "j", "j", "j", "j", "Enter");

        AssertSnapshot(state, 80, 24, "detail-expanded-80x24.txt");
    }

    [Fact]
    public void GivenTheDescriptionFolded_WhenBuildingATinyFrame_ThenNothingOverflows()
    {
        var state = Keys(Loaded(40, 10), "]", "]", "z", "a");

        var lines = FrameBuilder.Build(state, 40, 10, _keymap);

        Assert.All(lines, line => Assert.Equal(40, DisplayWidth.Of(line)));
        Assert.Contains(lines, line => line.Contains("folded"));
    }

    [Fact]
    public void GivenFocusOnAWrappedField_WhenRendering_ThenEveryRowOfItIsSelectedAndOnlyTheFirstHasTheMark()
    {
        var labels = Enumerable.Range(1, 12).Select(number => $"label-number-{number}").ToArray();
        var state = WithLabels(labels);
        var context = new BodyContext(_keymap);
        var lines = new IssueDetailRenderer().Render(state, context, 80, 24);
        var first = lines.ToList().FindIndex(line => line.PlainText.Contains("Labels"));

        Assert.StartsWith("│>Labels", lines[first].PlainText);
        Assert.DoesNotContain(lines[first + 1].PlainText, ">");

        foreach (var row in lines.Skip(first).Take(2))
        {
            Assert.Contains(row.Spans, span => span.Role == TextRole.Selected && span.Text.Trim().Length > 0);
            Assert.DoesNotContain(row.Spans, span => span.Role is TextRole.Strong or TextRole.Editable);
        }
    }

    [Fact]
    public void GivenFocusOnAnEditableFieldInTheAsciiSet_WhenBuilding_ThenTheMarkerIsAnAsciiVAndNoUnicodeTriangleRemains()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("j")).State;

        var lines = FrameBuilder.Build(state, 80, 24, _keymap, null, GlyphSet.Ascii);

        Assert.Contains(lines, line => line.StartsWith("|>Status    In Progress v"));
        Assert.DoesNotContain(lines, line => line.Contains('▾'));
    }

    [Fact]
    public void GivenOnlyTheListRowIsKnown_WhenRendering_ThenEditableFieldsStillShowAsFormRows()
    {
        var lines = FrameBuilder.Build(Opened(100, 24), 100, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Priority"));
        Assert.Contains(lines, line => line.Contains("Assignee") && line.Contains("Unassigned"));
    }

    [Fact]
    public void GivenAnOpenIssue_WhenBuilding_ThenTheBreadcrumbNamesTheViewAndTheKey()
    {
        var lines = FrameBuilder.Build(Loaded(160, 30), 160, 30, _keymap);

        Assert.Contains("My open issues › GCM-12", lines[^1]);
        Assert.StartsWith("╭─ GCM-12", lines[FrameBuilder.HeaderRows]);
    }

    [Fact]
    public void GivenAsciiGlyphs_WhenBuilding_ThenPanelBordersAreAsciiAndKeepTheirWidth()
    {
        var lines = FrameBuilder.Build(Loaded(80, 24), 80, 24, _keymap, null, GlyphSet.Ascii);

        var body = lines.Skip(FrameBuilder.HeaderRows).Take(11).ToList();
        Assert.StartsWith("+- GCM-12 - Task ", body[0]);
        Assert.StartsWith("|>Fix login", body[1]);
        Assert.StartsWith("| ", body[2]);
        Assert.StartsWith("+- Description ", body[10]);
        Assert.EndsWith("-+", body[0]);
        Assert.EndsWith("-+", body[10].TrimEnd());
        Assert.DoesNotContain(body, line => line.Any(character => character is '╭' or '│' or '─' or '╮'));
        Assert.Contains(lines, line => line.Contains("In Progress v"));
        Assert.DoesNotContain(lines, line => line.Contains('▾'));
        Assert.All(lines, line => Assert.Equal(80, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenOnlyTheRowIsKnown_WhenBuilding_ThenTheRowFieldsShowWithLoadingMarks()
    {
        var state = Opened(160, 30);

        var lines = FrameBuilder.Build(state, 160, 30, _keymap);

        Assert.Contains("Loading…", lines[^1]);
        Assert.Contains(lines, line => line.StartsWith("╭─ GCM-12 · Task "));
        Assert.Contains(lines, line => line.StartsWith("│>Row summary"));
    }

    [Fact]
    public void GivenScrolledDocument_WhenBuilding_ThenTheWindowStartsAtTheScrollAndTheRulerSaysSo()
    {
        var state = Loaded(160, 12);
        var bottom = _reducer.Reduce(state, new KeyPressed("G")).State;
        var middle = state;

        for (var page = 0; page < 3; page++)
        {
            middle = _reducer.Reduce(middle, new KeyPressed("Ctrl-d")).State;
        }


        var top = FrameBuilder.Build(state, 160, 12, _keymap);
        var end = FrameBuilder.Build(bottom, 160, 12, _keymap);
        var half = FrameBuilder.Build(middle, 160, 12, _keymap);

        Assert.Contains(" Top", top[^1]);
        Assert.Contains(" Bot", end[^1]);
        Assert.Matches(@" \d+%", half[^1]);
        Assert.NotEqual(top[FrameBuilder.HeaderRows + 1], end[FrameBuilder.HeaderRows + 1]);
        Assert.Contains("second paragraph", string.Join('\n', end));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 3)]
    [InlineData(20, 2)]
    public void GivenOpenIssue_WhenBuildingAtAnySize_ThenItNeverThrowsAndFillsTheFrame(int width, int height)
    {
        var lines = FrameBuilder.Build(Loaded(80, 24), width, height, _keymap);

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
    }

    [Theory]
    [InlineData(GlyphSet.Unicode)]
    [InlineData(GlyphSet.Ascii)]
    public void GivenOpenIssue_WhenBuildingAtEverySmallSize_ThenEveryLineKeepsTheWidth(GlyphSet glyphs)
    {
        var state = Loaded(80, 24);

        for (var width = 1; width <= 130; width++)
        {
            for (var height = 1; height <= 14; height++)
            {
                var lines = FrameBuilder.Build(state, width, height, _keymap, null, glyphs);

                Assert.Equal(height, lines.Count);
                Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
            }
        }
    }

    [Theory]
    [InlineData(100, 60)]
    [InlineData(160, 90)]
    public void GivenTallTerminalAndShortIssue_WhenBuilding_ThenTheClosingEdgeSitsDirectlyUnderTheLastLine(
        int width, int height)
    {
        var body = FrameBuilder.Build(LoadedShort(width, height), width, height, _keymap)
            .Skip(FrameBuilder.HeaderRows).ToList();
        var closing = body.FindIndex(static line => line.StartsWith('╰'));

        Assert.True(closing > 0);
        Assert.True(closing < 25, $"closing edge at row {closing}");
        Assert.EndsWith("╯", body[closing]);
        Assert.Contains("No comments", body[closing - 1]);
        Assert.All(body.Take(closing).Skip(1), line => Assert.StartsWith("│", line.Replace("├", "│")));
    }

    [Fact]
    public void GivenTallTerminalAndShortIssue_WhenScrolledToTheEnd_ThenTheClosingEdgeStillFollowsTheContent()
    {
        var state = _reducer.Reduce(LoadedShort(100, 60), new KeyPressed("G")).State;

        var body = FrameBuilder.Build(state, 100, 60, _keymap).Skip(FrameBuilder.HeaderRows).ToList();

        Assert.Single(body, static line => line.StartsWith('╰'));
        Assert.True(body.FindIndex(static line => line.StartsWith('╰')) < 25);
    }

    [Fact]
    public void GivenTallTerminalAndLongIssue_WhenBuilding_ThenTheClosingEdgeIsAtThePaneBottom()
    {
        var body = FrameBuilder.Build(Loaded(100, 30), 100, 30, _keymap).Skip(FrameBuilder.HeaderRows).ToList();

        Assert.StartsWith("╰", body[^2]);
    }

    [Fact]
    public void GivenScrolledOpenIssue_WhenBuilding_ThenTheTopEdgeNamesThePaneInViewAndTheFrameStaysClosed()
    {
        var state = _reducer.Reduce(Loaded(160, 12), new KeyPressed("G")).State;

        var body = FrameBuilder.Build(state, 160, 12, _keymap).Skip(FrameBuilder.HeaderRows).ToList();

        Assert.StartsWith("╭", body[0]);
        Assert.DoesNotContain("GCM-12", body[0]);
        Assert.Matches(@"^╭─ (Description|Comments)", body[0]);
        Assert.StartsWith("╰", body[^2]);
        Assert.EndsWith("╯", body[^2]);
    }

    [Fact]
    public void GivenASectionJump_WhenBuilding_ThenTheTopEdgeNamesTheSectionAndNoSecondEdgeFollowsIt()
    {
        var state = _reducer.Reduce(Loaded(160, 12), new KeyPressed("]")).State;

        state = _reducer.Reduce(state, new KeyPressed("]")).State;

        var body = FrameBuilder.Build(state, 160, 12, _keymap).Skip(FrameBuilder.HeaderRows).ToList();

        Assert.StartsWith("╭─ Description", body[0]);
        Assert.False(body[1].StartsWith('├'), body[1]);
    }

    [Fact]
    public void GivenNarrowTerminal_WhenOpenIssue_ThenTheRulerAndKeyStillShowInTheStatusBar()
    {
        var lines = FrameBuilder.Build(Loaded(80, 12), 80, 12, _keymap);

        Assert.Contains("GCM-12", lines[^1]);
        Assert.Contains("Top", lines[^1]);
        Assert.Contains("q quit", lines[^1]);
    }

    [Fact]
    public void GivenHelpOpenOverAnIssue_WhenBuilding_ThenHelpWins()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("?")).State;

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("╭─ Help"));

        // The view stays visible around and behind the popup (it is dimmed, never erased).
        Assert.Contains(lines, line => line.Contains("GCM-12 · Task"));
    }

    [Fact]
    public void GivenFrameBuiltForADifferentWidthThanTheDocument_WhenBuilding_ThenItIsRelaidOutForTheFrame()
    {
        var state = Loaded(80, 24);

        var lines = FrameBuilder.Build(state, 50, 24, _keymap);

        Assert.All(lines, line => Assert.Equal(50, DisplayWidth.Of(line)));
        Assert.Contains("Status    In Progress", string.Join('\n', lines));
    }

    private static void AssertSnapshot(AppState state, int width, int height, string name)
    {
        var lines = FrameBuilder.Build(state, width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";

        var expected = SnapshotFile.Expect(name, actual);

        Assert.Equal(expected, actual);
    }

    private AppState Opened(int width, int height)
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };

        state = _reducer.Reduce(state, new ViewportChanged(
            height - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows,
            width)).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "assignee = currentUser()", ViewLayout.List, "My open issues");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var page = new IssuePage([Row()], null, null);
        var loaded = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));

        return _reducer.Reduce(loaded.State, new KeyPressed("Enter")).State;
    }

    private AppState LoadedEpic(int width, int height)
    {
        var state = Opened(width, height);
        var tab = state.Session.Tabs[0];
        var detail = tab.Detail[0];
        var epicType = new IssueTypeSummary("1", "Epic", 1, false);
        var done = new IssueStatus("2", "Done", "done");
        var todo = new IssueStatus("1", "To Do", "new");
        var subtaskType = new IssueTypeSummary("3", "Sub-task", -1, true);
        var epic = Issue() with
        {
            Description = null,
            IssueType = epicType,
            Subtasks = [new IssueStub("GCM-18", "Write the migration", todo, subtaskType)],
            Links =
            [
                new IssueLink(
                    "1",
                    "Blocks",
                    IssueLinkDirection.Outward,
                    "blocks",
                    new IssueStub("GCM-30", "Launch", todo, _type)),
                new IssueLink(
                    "2",
                    "Blocks",
                    IssueLinkDirection.Inward,
                    "is blocked by",
                    new IssueStub("GCM-9", "Legal review", done, _type)),
            ],
            Attachments =
            [
                new IssueAttachment("1", "日本語のノート.txt", "text/plain", 47, _now.AddDays(-2), null, null),
                new IssueAttachment("2", "screenshot.png", "image/png", 3_500_000, _now.AddHours(-5), null, null),
            ],
        };
        var children = new IssuePage([Row("GCM-21", "Child story"), Row("GCM-22", "Another child")], null, null);
        var remote = new RemoteLink("9", "https://example.com/spec", "Design spec", "Confluence");

        state = _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, detail.IssueRequest, epic, _now)).State;
        state = _reducer.Reduce(state, new IssueChildrenLoaded(tab.Id, Active(state).ChildrenRequest, children, _now)).State;
        var noComments = new OffsetPage<IssueComment>([], 0, DetailPaging.CommentPageSize, 0, true);

        state = _reducer.Reduce(state, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, noComments, _now)).State;

        return _reducer.Reduce(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, [remote], _now)).State;
    }

    [Fact]
    public void GivenAnOpenedIssue_WhenBuilding_ThenTheCursorLineEndsInTheRowMenuMarker()
    {
        var frame = FrameBuilder.BuildStyled(Loaded(80, 24), 80, 24, _keymap)!;
        var marker = Assert.IsType<OverlayRegion>(frame.RowMenuMarker);
        var line = frame.Body[marker.Top - FrameBuilder.HeaderRows].PlainText;

        Assert.Equal(RowMenuMarker.Text, line.Substring(marker.Left, marker.Width));
        Assert.Single(frame.Body, candidate => candidate.PlainText.Contains(RowMenuMarker.Text));
    }

    [Fact]
    public void GivenTheHeaderPane_WhenBuilding_ThenTheStatusChipKeepsItsCategoryRoleAtTheRightOfTheTopEdge()
    {
        var top = FrameBuilder.BuildStyled(Loaded(80, 24), 80, 24, _keymap)!.Body[0];

        var status = Assert.Single(top.Spans, span => span.Text.Contains("In Progress"));
        Assert.Equal(TextRole.StatusInProgress, status.Role);
        Assert.EndsWith("In Progress ─╮", top.PlainText);
    }

    [Fact]
    public void GivenScrolledToAnotherPane_WhenBuilding_ThenTheTopEdgeHasNoStatusChip()
    {
        var state = _reducer.Reduce(Loaded(80, 14), new KeyPressed("G")).State;

        var top = FrameBuilder.BuildStyled(state, 80, 14, _keymap)!.Body[0];

        Assert.DoesNotContain("In Progress", top.PlainText);
    }

    [Fact]
    public void GivenTheCursorInTheFirstPane_WhenBuilding_ThenItsBoxIsFocusedAndTheNextEdgeChipIsDim()
    {
        var body = FrameBuilder.BuildStyled(Loaded(80, 24), 80, 24, _keymap)!.Body;

        var top = body[0].Spans;
        var edge = Assert.Single(body, line => line.PlainText.StartsWith("├─ Description"));
        var side = body.First(line => line.PlainText.StartsWith('│'));

        Assert.Contains(top, span => span.Role == TextRole.ChipFocus);
        Assert.Contains(top, span => span.Role == TextRole.FrameFocus);
        Assert.Equal(TextRole.FrameFocus, side.Spans[0].Role);
        Assert.Equal(TextRole.FrameFocus, edge.Spans[0].Role);
        Assert.DoesNotContain(edge.Spans, span => span.Role == TextRole.ChipFocus);
        Assert.Contains(edge.Spans, span => span.Role == TextRole.Muted && span.Text.Contains("Description"));
    }

    [Fact]
    public void GivenTheCursorMovedToTheNextPane_WhenBuilding_ThenTheFocusFollowsIntoThatPaneAndItsTitleChip()
    {
        var state = _reducer.Reduce(Loaded(80, 14), new KeyPressed("]")).State;

        state = _reducer.Reduce(state, new KeyPressed("]")).State;

        var body = FrameBuilder.BuildStyled(state, 80, 14, _keymap)!.Body;

        Assert.Contains(body[0].Spans, span => span.Role == TextRole.ChipFocus && span.Text.Contains("Description"));
        Assert.Equal(TextRole.FrameFocus, body[0].Spans[0].Role);
        Assert.DoesNotContain(
            body,
            line => line.Spans.Any(span => span.Role == TextRole.Border && span.Text.Contains('│')));
    }

    [Fact]
    public void GivenTheCursorInLowerPanes_WhenBuilding_ThenAtMostOnePaneHasAFilledChipAndTheTopEdgeAgreesWithIt()
    {
        var state = Loaded(80, 12);

        for (var step = 0; step < 9; step++)
        {
            state = _reducer.Reduce(state, new KeyPressed("j")).State;
        }

        var body = FrameBuilder.BuildStyled(state, 80, 12, _keymap)!.Body;

        Assert.True(body.Count(line => line.Spans.Any(span => span.Role == TextRole.ChipFocus)) <= 1);
        Assert.Equal(
            body[0].Spans.Any(span => span.Role == TextRole.ChipFocus),
            body[0].Spans[0].Role == TextRole.FrameFocus);
    }

    private static DetailState Active(AppState state)
    {
        return state.Session.Tabs[0].Detail[0];
    }

    private AppState LoadedShort(int width, int height)
    {
        var state = Opened(width, height);
        var tab = state.Session.Tabs[0];
        var detail = tab.Detail[0];
        var page = new OffsetPage<IssueComment>([], 0, DetailPaging.CommentPageSize, 0, true);

        state = _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue() with { Description = null }, _now)).State;

        state = _reducer.Reduce(state, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now)).State;

        return _reducer.Reduce(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, [], _now)).State;
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key)).State);
    }

    private AppState WithLabels(string[] labels)
    {
        var state = Opened(80, 24);
        var tab = state.Session.Tabs[0];
        var detail = tab.Detail[0];

        state = _reducer.Reduce(
            state,
            new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue() with { Labels = labels }, _now)).State;

        while (state.Session.Tabs[0].Detail[0].Document!.Targets[state.Session.Tabs[0].Detail[0].Cursor]?.Value != "labels")
        {
            state = _reducer.Reduce(state, new KeyPressed("j")).State;
        }

        return state;
    }

    private AppState Loaded(int width, int height)
    {
        var state = Opened(width, height);
        var tab = state.Session.Tabs[0];
        var detail = tab.Detail[0];
        var comments = new[]
        {
            Comment("c1", "Jane Doe", "Found it: the redirect target is dropped after the refresh.", TimeSpan.FromDays(1)),
            Comment("c2", "日本語の作者", "日本語のコメント。これは長い文章で、折り返しが必要になります。", TimeSpan.FromHours(3)),
            Comment("c3", "Ola Nordmann", "See also café and 😀 in the second paragraph, which is long enough to wrap.", TimeSpan.FromMinutes(5)),
        };
        var page = new OffsetPage<IssueComment>(comments, 0, DetailPaging.CommentPageSize, 3, true);

        state = _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now)).State;

        state = _reducer.Reduce(state, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now)).State;

        return _reducer.Reduce(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, [], _now)).State;
    }

    private static IssueSummary Row(string key = "GCM-12", string summary = "Row summary")
    {
        return new IssueSummary(
            "1",
            key,
            summary,
            _status,
            _type,
            null,
            null,
            [],
            null,
            null,
            "GCM");
    }

    private static IssueDetail Issue()
    {
        using var document = JsonDocument.Parse(RecordedFixtures.Read("adf/issue-gtm5-rich-description.json"));
        var description = AdfParser.Parse(document.RootElement.GetProperty("fields").GetProperty("description"));

        return new IssueDetail(
            "1",
            "GCM-12",
            "Fix login redirect after token refresh",
            _status,
            _type,
            "High",
            new IssueAssignee("j", "Jane Doe"),
            new IssueAssignee("o", "Ola Nordmann"),
            ["auth", "bug-bash"],
            _now.AddDays(-9),
            _now.AddHours(-2),
            "GCM",
            description,
            new IssueStub("GCM-17", "Reporting overhaul", _status, _type),
            [],
            [],
            []);
    }

    private static IssueComment Comment(string id, string author, string text, TimeSpan age)
    {
        var body = AdfParser.Parse(
            $$"""{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"{{text}}"}]}]}""");
        var when = _now - age;

        return new IssueComment(id, new IssueAssignee(id, author), body, when, when);
    }
}
