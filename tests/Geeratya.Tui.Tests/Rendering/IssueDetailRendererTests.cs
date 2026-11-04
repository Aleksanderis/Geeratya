using System.Globalization;
using System.Text;
using System.Text.Json;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Panes;
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
using Geeratya.Jira.Transitions.Models;
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
    public void GivenTheCursorOnACommentBar_WhenBuilding_ThenTheMarkerSitsBeforeTheTimeAndTheTimeStaysWhole()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("G")).State;
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var marker = Assert.IsType<OverlayRegion>(frame.RowMenuMarker);
        var line = frame.Body[marker.Top - FrameBuilder.HeaderRows].PlainText;

        Assert.Contains("Ola Nordmann", line);
        Assert.Contains("... 5 min ago │", line);
        Assert.Equal(RowMenuMarker.Text, line.Substring(marker.Left, marker.Width));
    }

    [Fact]
    public void GivenPrebuiltCommentRows_WhenBuildingTheDocument_ThenThoseRowsAreUsed()
    {
        var loaded = Loaded(80, 24);
        var top = loaded.Session.Tabs.First(tab => tab.Id == loaded.Session.Active).Detail[^1];
        var width = top.Document!.Width;
        var rows = DetailZoom.CommentRowsOf(top, width);
        var marked = rows.Select(row => row with { Line = StyledLine.Of("PREBUILT") }).ToList();

        var document = IssueDocumentBuilder.Build(top, width, top.Document.ViewportRows, marked);

        Assert.Contains(document.Lines, line => line.PlainText.Contains("PREBUILT"));
        Assert.DoesNotContain(IssueDocumentBuilder.Build(top, width, top.Document.ViewportRows).Lines, line => line.PlainText.Contains("PREBUILT"));
    }

    [Fact]
    public void GivenCursorMoves_WhenBuilding_ThenOnlyTheFocusedFieldRowCarriesTheMarkerAndEdgesAreSkipped()
    {
        var state = OnField(Loaded(80, 24), DetailFieldIds.Status);

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        var marked = lines.Where(line => line.Contains(FieldBlock.Bar)).ToList();
        Assert.Single(marked);
        Assert.Contains("In Progress", marked[0]);
        Assert.DoesNotContain(lines, line => line.StartsWith('>'));
        Assert.Contains(lines, line => line.StartsWith("╭─ Details"));
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
        var state = OnField(Loaded(80, 24), DetailFieldIds.Status);
        var context = new BodyContext(_keymap);

        var lines = new IssueDetailRenderer().Render(state, context, 80, 24);

        var status = lines.Single(line => line.PlainText.Contains("In Progress ▾"));
        var priority = lines.Single(line => line.PlainText.Contains("High"));
        Assert.Contains(status.Spans, span => span.Role == TextRole.SelectionBar);
        Assert.Contains(status.Spans, span => span.Role == TextRole.Selected && span.Text.Contains("In Progress"));
        Assert.Contains(status.Spans, span => span.Role == TextRole.Selected && span.Text.Contains('▾'));
        Assert.DoesNotContain(priority.Spans, span => span.Role is TextRole.Selected or TextRole.SelectionBar);
    }

    [Fact]
    public void GivenTheDescriptionExpanded_WhenBuildingTheFrame_ThenItShowsAllLinesAndMatchesTheSnapshot()
    {
        var cut = OnLineWith(Keys(Loaded(80, 24), "Ctrl-j"), "more lines");
        var state = Keys(cut, "Enter");

        AssertSnapshot(state, 80, 24, "detail-expanded-80x24.txt");
    }

    [Fact]
    public void GivenTheDescriptionCut_WhenBuildingATinyFrame_ThenNothingOverflows()
    {
        var state = Keys(Loaded(40, 10), "Ctrl-j");

        var lines = FrameBuilder.Build(state, 40, 10, _keymap);

        Assert.All(lines, line => Assert.Equal(40, DisplayWidth.Of(line)));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(80)]
    [InlineData(104)]
    [InlineData(120)]
    public void GivenAnEpicWithEveryPane_WhenBuildingAtThePlannedWidths_ThenEveryRowKeepsTheWidthInBothGlyphSets(
        int width)
    {
        var state = LoadedEpic(width, 36);

        foreach (var glyphs in new[] { GlyphSet.Unicode, GlyphSet.Ascii })
        {
            var lines = FrameBuilder.Build(state, width, 36, _keymap, null, glyphs);

            Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
            Assert.Contains(lines, line => line.Contains("Sub-tasks 1"));
            Assert.Contains(lines, line => line.Contains("GCM-18"));
        }
    }

    [Fact]
    public void GivenFocusOnAWrappedField_WhenRendering_ThenEveryRowOfItIsSelectedAndOnlyTheFirstHasTheMark()
    {
        var labels = Enumerable.Range(1, 12).Select(number => $"label-number-{number}").ToArray();
        var state = WithLabels(labels);
        var context = new BodyContext(_keymap);
        var lines = new IssueDetailRenderer().Render(state, context, 80, 24);
        var first = lines.ToList().FindIndex(line => line.PlainText.Contains("Labels"));

        Assert.DoesNotContain(lines[first].Spans, span => span.Role is TextRole.Selected or TextRole.SelectionBar);

        foreach (var row in lines.Skip(first + 1).Take(2))
        {
            Assert.Contains(row.Spans, span => span.Role == TextRole.SelectionBar);
            Assert.Contains(row.Spans, span => span.Role == TextRole.Selected);
            Assert.DoesNotContain(row.Spans, span => span.Role is TextRole.ValueBlock);
        }
    }

    [Fact]
    public void GivenFocusOnAnEditableFieldInTheAsciiSet_WhenBuilding_ThenTheMarkerIsAnAsciiVAndNoUnicodeTriangleRemains()
    {
        var state = OnField(Loaded(80, 24), DetailFieldIds.Status);

        var lines = FrameBuilder.Build(state, 80, 24, _keymap, null, GlyphSet.Ascii);

        Assert.Contains(lines, line => line.StartsWith("| >* In Progress v") || line.StartsWith("|>* In Progress v"));
        Assert.DoesNotContain(lines, line => line.Contains('▾') || line.Contains('●'));
    }

    [Fact]
    public void GivenOnlyTheListRowIsKnown_WhenRendering_ThenEditableFieldsStillShowAsFormRows()
    {
        var lines = FrameBuilder.Build(Opened(120, 40), 120, 40, _keymap);

        Assert.Contains(lines, line => line.Contains("Priority"));
        Assert.Contains(lines, line => line.Contains("[--] Unassigned"));
    }

    [Fact]
    public void GivenAnOpenIssue_WhenBuilding_ThenTheBreadcrumbNamesTheViewAndTheKey()
    {
        var lines = FrameBuilder.Build(Loaded(160, 30), 160, 30, _keymap);

        Assert.Contains("My open issues › GCM-12", lines[^1]);
        Assert.StartsWith("╭─ GCM-12", Overlay(lines, 160, 30)[0]);
    }

    [Fact]
    public void GivenAsciiGlyphs_WhenBuilding_ThenPanelBordersAreAsciiAndKeepTheirWidth()
    {
        var lines = FrameBuilder.Build(Loaded(80, 24), 80, 24, _keymap, null, GlyphSet.Ascii);

        var body = lines.Skip(FrameBuilder.HeaderRows).Take(11).ToList();
        Assert.StartsWith("+- GCM-12 - Task ", body[0]);
        Assert.StartsWith("|>Fix login", body[1]);
        Assert.StartsWith("+--", body[2]);
        Assert.StartsWith("+- Description ", body[3]);
        Assert.Contains("1-3 of", body[3]);
        Assert.EndsWith("-+", body[0]);
        Assert.EndsWith("-+", body[2].TrimEnd());
        Assert.Contains(body, line => line.Contains("^ |") || line.Contains("# |") || line.Contains(". |"));
        Assert.DoesNotContain(body, line => line.Any(character => character is '╭' or '│' or '─' or '╮'));
        Assert.DoesNotContain(lines, line => line.Contains('▾') || line.Contains('●'));
        Assert.All(lines, line => Assert.Equal(80, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenOnlyTheRowIsKnown_WhenBuilding_ThenTheRowFieldsShowWithLoadingMarks()
    {
        var state = Opened(160, 30);

        var lines = FrameBuilder.Build(state, 160, 30, _keymap);

        Assert.Contains("Loading…", lines[^1]);
        var overlay = Overlay(lines, 160, 30);

        Assert.Contains(overlay, line => line.StartsWith("╭─ GCM-12 · Task "));
        Assert.Contains(overlay, line => line.StartsWith("│>Row summary"));
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
        Assert.Contains("second paragraph", string.Join('\n', Overlay(end, 160, 12)));
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
    public void GivenTallTerminalAndShortIssue_WhenBuilding_ThenTheLastPaneClosesDirectlyUnderItsLastLine(
        int width, int height)
    {
        var box = DetailOverlayGeometry.Place(width, FrameBuilder.BodyHeight(height));
        var sidebar = DetailLayout.HasSidebar(box.Width);
        var mainWidth = sidebar ? DetailLayout.MainWidth(box.Width) : box.Width;
        var body = Overlay(FrameBuilder.Build(LoadedShort(width, height), width, height, _keymap), width, height)
            .Select(line => line[..mainWidth])
            .ToList();
        var closing = body.FindLastIndex(static line => line.StartsWith('╰'));

        Assert.True(closing > 0);
        Assert.True(closing < 45, $"closing edge at row {closing}");
        Assert.EndsWith("╯", body[closing]);
        Assert.Contains(sidebar ? "No comments" : "Updated", body[closing - 1]);
        Assert.All(body.Take(closing + 1), line => Assert.Contains(line[0], "│╭╰"));
    }

    [Fact]
    public void GivenTallTerminalAndShortIssue_WhenScrolledToTheEnd_ThenTheClosingEdgeStillFollowsTheContent()
    {
        var state = _reducer.Reduce(LoadedShort(100, 60), new KeyPressed("G")).State;

        var body = Overlay(FrameBuilder.Build(state, 100, 60, _keymap), 100, 60);

        Assert.True(body.FindLastIndex(static line => line.StartsWith('╰')) < 45);
    }

    [Fact]
    public void GivenALongIssue_WhenScrolledToTheEnd_ThenTheLastPaneClosesAtTheOverlayBottom()
    {
        var state = Keys(Loaded(100, 30), "G", "j", "j", "j", "j", "j", "j", "j", "j", "j", "j", "j", "j");

        var body = Overlay(FrameBuilder.Build(state, 100, 30, _keymap), 100, 30);

        Assert.StartsWith("╰", body[^1]);
        Assert.EndsWith("╯", body[^1]);
    }

    [Fact]
    public void GivenAPaneJump_WhenBuilding_ThenTheWindowStartsAtTheTopEdgeOfThatPane()
    {
        var state = _reducer.Reduce(Loaded(160, 12), new KeyPressed("Ctrl-j")).State;

        var body = Overlay(FrameBuilder.Build(state, 160, 12, _keymap), 160, 12);

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
        Assert.Contains("GCM-12", string.Join('\n', lines));
    }

    [Fact]
    public void GivenOpenIssueAt120Columns_WhenBuilding_ThenTheViewShowsMutedAroundTheOverlay()
    {
        var frame = FrameBuilder.BuildStyled(Loaded(120, 36), 120, 36, _keymap)!;

        Assert.Contains("My open issues", frame.Body[0].PlainText);
        Assert.All(frame.Body[0].Spans, span => Assert.True(span.Ground.HasFlag(SpanGround.Dim)));
        Assert.StartsWith("╭─ GCM-12", frame.Body[4].PlainText[2..]);
        Assert.All(frame.Body[^1].Spans, span => Assert.True(span.Ground.HasFlag(SpanGround.Dim)));
    }

    [Fact]
    public void GivenOpenIssueBelow100Columns_WhenBuilding_ThenTheOverlayFillsTheBodyWithNoView()
    {
        var frame = FrameBuilder.BuildStyled(Loaded(99, 30), 99, 30, _keymap)!;

        Assert.StartsWith("╭─ GCM-12", frame.Body[0].PlainText);
        Assert.DoesNotContain(frame.Body, line => line.PlainText.Contains("My open issues"));
    }

    [Fact]
    public void GivenOpenIssueAndTheProfileManager_WhenBuilding_ThenTheManagerWinsOverTheDetail()
    {
        var state = Loaded(120, 36) with { ProfileList = new ProfileListState(0) };

        var lines = FrameBuilder.Build(state, 120, 36, _keymap);

        Assert.DoesNotContain(lines, line => line.Contains("╭─ GCM-12"));
        Assert.Contains(lines, line => line.Contains("Profiles"));
    }

    [Fact]
    public void GivenOneLineHiddenBelowTheOverlay_WhenReadingTheRuler_ThenItShowsAPercentageNotBot()
    {
        var state = Loaded(120, 20);
        var top = state.Session.Tabs[0].Detail[^1];
        var rows = DetailLayout.ViewportRows(DetailSize.ListRows(state.ViewportColumns, state.ViewportBodyHeight, 0));
        var scrolled = top with { Scroll = top.Document!.Lines.Count - rows - 1 };
        var tab = state.Session.Tabs[0] with { Detail = [scrolled] };
        var shown = state with { Session = state.Session with { Tabs = [tab] } };

        var lines = FrameBuilder.Build(shown, 120, 20, _keymap);

        Assert.DoesNotContain(" Bot", lines[^1]);
        Assert.Matches(@" \d+%", lines[^1]);
    }

    [Fact]
    public void GivenThePriorityDropdownIsOpen_WhenBuildingTheFrame_ThenItHangsUnderTheValueAndMatchesTheSnapshot()
    {
        AssertSnapshot(PriorityDropdown(120, 36), 120, 36, "detail-dropdown-120x36.txt");
    }

    [Fact]
    public void GivenThePriorityDropdownIsOpen_WhenBuilding_ThenTheDetailBehindItIsNotDimmed()
    {
        var frame = FrameBuilder.BuildStyled(PriorityDropdown(120, 36), 120, 36, _keymap)!;

        Assert.Contains(frame.Body, line => line.PlainText.Contains("> Highest"));
        var heading = frame.Body.First(line => line.PlainText.Contains("Heading one"));

        Assert.DoesNotContain(
            heading.Spans.Where(span => span.Text.Contains("Heading one")),
            span => span.Ground.HasFlag(SpanGround.Dim));
    }

    [Theory]
    [InlineData(GlyphSet.Unicode)]
    [InlineData(GlyphSet.Ascii)]
    public void GivenThePriorityDropdownIsOpen_WhenBuildingAtAnyHeight_ThenEveryLineKeepsTheWidthAndTheRowsShow(
        GlyphSet glyphs)
    {
        for (var height = 10; height <= 44; height += 2)
        {
            var lines = FrameBuilder.Build(PriorityDropdown(120, height), 120, height, _keymap, null, glyphs);

            Assert.All(lines, line => Assert.Equal(120, DisplayWidth.Of(line)));
            Assert.Contains(lines, line => line.Contains("Highest"));
        }
    }

    [Fact]
    public void GivenAFieldWithoutEditMetadata_WhenBuildingTheSidebar_ThenOnlyTheStatusBlockCarriesTheMark()
    {
        var lines = FrameBuilder.Build(Loaded(120, 36), 120, 36, _keymap);

        Assert.Single(lines, line => line.Contains('▾'));
    }

    [Fact]
    public void GivenFocusOnTheSidebar_WhenBuilding_ThenOnlyTheSidebarFrameIsFocusedAndTheFieldShowsTheBar()
    {
        var state = Keys(Loaded(120, 36), "Ctrl-l");

        var body = FrameBuilder.BuildStyled(state, 120, 36, _keymap)!.Body;
        var details = body.First(line => line.PlainText.Contains("Details"));

        Assert.Contains(body, line => line.Spans.Any(span => span.Role == TextRole.SelectionBar));
        Assert.Contains(details.Spans, span => span.Role == TextRole.ChipFocus && span.Text.Contains("Details"));
        Assert.DoesNotContain(
            body.Where(line => line.PlainText.Contains("Description")),
            line => line.Spans.Any(span => span.Role == TextRole.ChipFocus));
    }

    [Fact]
    public void GivenANavigatingFormWithPanes_WhenCtrlWIsPending_ThenTheStatusLineEchoesIt()
    {
        var state = Keys(Loaded(120, 36), "Space", "i", "e");
        var tab = state.Session.Tabs[0];
        var summary = FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, []);
        var labels = FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Text, false, []);
        state = _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [summary, labels])).State;
        Assert.True(state.Session.Tabs[0].Form!.Navigating);
        var grid = new PaneGrid([new PaneSlot("title", 0, 0)]);
        var open = state.Session.Tabs[0];
        state = state with
        {
            Session = state.Session with { Tabs = [open with { Panes = new PaneState(grid) }] },
        };

        var pending = Keys(state, "Ctrl-w");

        Assert.Equal("Ctrl-w", pending.PendingKeys);
        Assert.Contains("INSERT  Ctrl-w", StatusBar.Render(pending, 120, _keymap));
    }

    private AppState PriorityDropdown(int width, int height)
    {
        var state = Loaded(width, height);
        var tab = state.Session.Tabs[0];

        state = _reducer.Reduce(
            state,
            new IssueEditableLoaded(tab.Id, "GCM-12", [DetailFieldIds.Priority, DetailFieldIds.Labels])).State;
        state = Keys(state, "Ctrl-l", "j", "Enter");

        var form = state.Session.Tabs[0].Form!;
        FieldOption[] options =
        [
            new("1", "Highest"),
            new("2", "High"),
            new("3", "Medium"),
            new("4", "Low"),
            new("5", "Lowest"),
        ];
        var field = FormField.Create(FormFieldIds.Priority, "Priority", FormFieldKind.Select, false, options)
            .WithInitial(FormValue.OfChosen(["2"]));

        state = _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, form.RequestId, [field])).State;

        return Keys(state, "Down", "Down");
    }

    // The rows of the detail overlay with the margin and the backdrop around it cut away.
    private static List<string> Overlay(IReadOnlyList<string> frame, int width, int height)
    {
        var box = DetailOverlayGeometry.Place(width, FrameBuilder.BodyHeight(height));

        return
        [
            .. frame.Skip(FrameBuilder.HeaderRows + box.Top).Take(box.Height)
                .Select(line => Columns(line, box.Left, box.Width)),
        ];
    }

    // The cells from column start for width columns; wide characters count two.
    private static string Columns(string line, int start, int width)
    {
        var builder = new StringBuilder();
        var column = 0;
        var elements = StringInfo.GetTextElementEnumerator(line);

        while (elements.MoveNext())
        {
            var element = (string)elements.Current;

            if (column >= start && column < start + width)
            {
                builder.Append(element);
            }

            column += DisplayWidth.Of(element);
        }

        return builder.ToString();
    }

    [Fact]
    public void GivenAZoomedPane_WhenDrawing_ThenTheStripIsTheFirstOverlayRowAndNamesTheCurrentSection()
    {
        var state = Keys(Loaded(120, 36), "Ctrl-j", "Space", "z");
        var lines = FrameBuilder.Build(state, 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("Description") && line.Contains("Comments 3"));
        Assert.Contains(lines, line => line.Contains("Description · zoomed"));
        Assert.DoesNotContain(lines, line => line.Contains("Comments 3 ─"));
    }

    [Fact]
    public void GivenAStripWiderThanTheScreen_WhenSwitchingToTheLastSection_ThenTheCurrentSectionStaysInView()
    {
        var state = Keys(Loaded(40, 14), "Ctrl-j", "Space", "z", "Ctrl-l", "Ctrl-l", "Ctrl-l", "Ctrl-l", "Ctrl-l");
        var lines = FrameBuilder.Build(state, 40, 14, _keymap);

        Assert.Contains(lines, line => line.Contains(" Details ") && !line.Contains("Description"));
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key)).State);
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
            width,
            FrameBuilder.BodyHeight(height))).State;

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
    public void GivenAnOpenedIssueInTheWideOverlay_WhenBuilding_ThenTheMarkerStaysInsideTheOverlayBox()
    {
        var frame = FrameBuilder.BuildStyled(Loaded(120, 36), 120, 36, _keymap)!;
        var marker = Assert.IsType<OverlayRegion>(frame.RowMenuMarker);
        var box = DetailOverlayGeometry.Place(120, FrameBuilder.BodyHeight(36));
        var line = frame.Body[marker.Top - FrameBuilder.HeaderRows].PlainText;

        Assert.True(marker.Left + marker.Width < box.Left + box.Width);
        Assert.EndsWith("...│ │", line.TrimEnd());
    }

    [Fact]
    public void GivenTheTitlePane_WhenBuilding_ThenTheStatusKeepsItsCategoryRoleAtTheRightOfTheSummaryRow()
    {
        var state = _reducer.Reduce(Loaded(80, 24), new KeyPressed("j")).State;
        var row = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!.Body[1];

        var status = Assert.Single(row.Spans, span => span.Text.Contains("In Progress"));
        Assert.Equal(TextRole.StatusInProgress, status.Role);
        Assert.EndsWith("In Progress │", row.PlainText);
        Assert.StartsWith("│ Fix login", row.PlainText);
    }

    [Fact]
    public void GivenTheCursorInTheFirstPane_WhenBuilding_ThenItsBoxIsFocusedAndTheNextPaneIsQuiet()
    {
        var body = FrameBuilder.BuildStyled(Loaded(80, 24), 80, 24, _keymap)!.Body;

        var top = body[0].Spans;
        var edge = Assert.Single(body, line => line.PlainText.StartsWith("╭─ Description"));
        var side = body.First(line => line.PlainText.StartsWith('│'));

        Assert.Contains(top, span => span.Role == TextRole.ChipFocus);
        Assert.Contains(top, span => span.Role == TextRole.FrameFocus);
        Assert.Equal(TextRole.FrameFocus, side.Spans[0].Role);
        Assert.Equal(TextRole.Border, edge.Spans[0].Role);
        Assert.DoesNotContain(edge.Spans, span => span.Role == TextRole.ChipFocus);
        Assert.Contains(edge.Spans, span => span.Role == TextRole.Muted && span.Text.Contains("Description"));
    }

    [Fact]
    public void GivenTheCursorMovedToTheNextPane_WhenBuilding_ThenTheFocusFollowsIntoThatPaneAndItsTitleChip()
    {
        var state = _reducer.Reduce(Loaded(80, 14), new KeyPressed("Ctrl-j")).State;

        var body = FrameBuilder.BuildStyled(state, 80, 14, _keymap)!.Body;

        Assert.Contains(body[0].Spans, span => span.Role == TextRole.ChipFocus && span.Text.Contains("Description"));
        Assert.Equal(TextRole.FrameFocus, body[0].Spans[0].Role);
        Assert.Equal(TextRole.FrameFocus, body[1].Spans[0].Role);
    }

    [Fact]
    public void GivenTheCursorInLowerPanes_WhenBuilding_ThenAtMostOnePaneHasAFilledChipAndItsFrameIsFocused()
    {
        var state = Loaded(80, 12);

        for (var step = 0; step < 9; step++)
        {
            state = _reducer.Reduce(state, new KeyPressed("j")).State;
        }

        var body = FrameBuilder.BuildStyled(state, 80, 12, _keymap)!.Body;
        var filled = body.Where(line => line.Spans.Any(span => span.Role == TextRole.ChipFocus)).ToList();

        Assert.True(filled.Count <= 1);
        Assert.All(filled, line => Assert.Equal(TextRole.FrameFocus, line.Spans[0].Role));
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

    private AppState OnField(AppState state, string field)
    {
        return OnLine(state, current => Document(current).Targets[Active(current).Cursor]?.Value == field);
    }

    private AppState OnLineWith(AppState state, string text)
    {
        return OnLine(state, current => Document(current).Lines[Active(current).Cursor].PlainText.Contains(text));
    }

    private AppState OnLine(AppState state, Func<AppState, bool> found)
    {
        for (var step = 0; step < 200 && !found(state); step++)
        {
            state = _reducer.Reduce(state, new KeyPressed("j")).State;
        }

        Assert.True(found(state));

        return state;
    }

    private static DetailDocument Document(AppState state)
    {
        return Active(state).Document!;
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
