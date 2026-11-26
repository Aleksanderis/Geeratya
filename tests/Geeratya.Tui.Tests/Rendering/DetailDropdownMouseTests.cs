using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The mouse on a dropdown that hangs under a Details field and on the title in its edit state (#714), through the
/// drawn frame's regions, the resolver and the reducer: a click on a row accepts it, a click outside closes it, a click
/// elsewhere while a pane is edited in place is ignored.
/// </summary>
[Trait("Os", "Independent")]
public class DetailDropdownMouseTests
{
    private const int Width = 120;
    private const int Height = 36;

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);
    private static readonly FieldOption[] _priorities =
        [new("1", "Highest"), new("2", "High"), new("3", "Medium"), new("4", "Low"), new("5", "Lowest")];

    private readonly AppReducer _reducer = new(_keymap);

    [Fact]
    public void GivenAPriorityDropdown_WhenClickingARow_ThenThatChoiceIsWrittenThroughTheEditPath()
    {
        var state = PriorityDropdown();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.FormChoice).Single(candidate => candidate.Index == 4);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var change = Assert.Single(Assert.IsType<EditRequest>(write.Request).Changes);
        Assert.Equal(FormFieldIds.Priority, change.FieldId);
        Assert.Equal(["4"], change.Value.Chosen);
    }

    [Fact]
    public void GivenAPriorityDropdown_WhenClickingOutsideIt_ThenItClosesAndTheIssueStaysOpen()
    {
        var state = PriorityDropdown();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var context = MouseContext.From(state, Width, Height, FrameBuilder.HeaderRows, _now, frame.Regions);

        var commands = MouseResolver.Resolve(context, 20, FrameBuilder.HeaderRows + 8, MouseGesture.Click, _now);
        var closed = commands.Aggregate(state, (current, command) => _reducer.Reduce(current, command).State);

        Assert.NotEmpty(commands);
        Assert.Null(closed.Session.Tabs[0].Form);
        Assert.Single(closed.Session.Tabs[0].Detail);
    }

    [Fact]
    public void GivenTheTitleInItsEditState_WhenClickingAnotherDetailRowInTheDrawnFrame_ThenNothingHappens()
    {
        var state = TitleEditing();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var box = DetailOverlayGeometry.Place(Width, FrameBuilder.BodyHeight(Height));
        var context = MouseContext.From(state, Width, Height, FrameBuilder.HeaderRows, _now, frame.Regions);

        var inside = MouseResolver.Resolve(
            context,
            box.Left + 10,
            FrameBuilder.HeaderRows + box.Top + 6,
            MouseGesture.Click,
            _now);
        var backdrop = MouseResolver.Resolve(context, 0, FrameBuilder.HeaderRows + 8, MouseGesture.Click, _now);

        Assert.Empty(inside);
        Assert.Empty(backdrop);
        Assert.Equal(PaneIds.Title, state.Session.Tabs[0].Form!.Pane);
    }

    [Fact]
    public void GivenARefusedDropdownWrite_WhenDrawn_ThenTheRefusalIsWrappedUnderTheFieldNotCut()
    {
        var state = PriorityDropdown();
        var tab = state.Session.Tabs[0];
        var field = tab.Form!.Fields[0] with { Error = "Must be created in the same project as the parent (GCM-12 → Priority High)" };
        var refused = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = tab.Form with { Fields = [field] } }] },
        };

        var frame = FrameBuilder.BuildStyled(refused, Width, Height, _keymap)!;

        var text = string.Join(' ', frame.Body.Select(line => string.Concat(line.Spans.Select(span => span.Text))));
        Assert.Contains("Must be", text, StringComparison.Ordinal);
        Assert.Contains("High)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTitleInItsEditState_WhenDoubleClickingAWordInTheDrawnFrame_ThenThatWordIsSelected()
    {
        var state = TitleEditing();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var cell = TextCell(frame, "login", 2);

        var result = RegionProbe.At(state, frame, cell.Column, cell.Row, MouseGesture.DoubleClick)!;

        Assert.Equal("login", FormEditor.SelectedText(result.State.Session.Tabs[0].Form!.Fields[0]));
    }

    [Fact]
    public void GivenTheDescriptionInItsEditState_WhenDraggingAcrossRows_ThenTheTextBetweenIsSelected()
    {
        var state = DescriptionEditing("alpha beta\n\ngamma delta");
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var from = TextCell(frame, "beta", 1);
        var to = TextCell(frame, "gamma", 2);
        var context = MouseContext.From(state, Width, Height, FrameBuilder.HeaderRows, _now, frame.Regions);

        var commands = MouseResolver.ResolveDrag(context, from.Column, from.Row, to.Column, to.Row, _now);
        var result = _reducer.Reduce(state, Assert.Single(commands)).State;

        Assert.Equal("eta\n\nga", FormEditor.SelectedText(result.Session.Tabs[0].Form!.Fields[0]));
    }

    [Fact]
    public void GivenTheDescriptionInItsEditState_WhenReadingTheTextRegions_ThenEachStartsWhereItsTextIsDrawn()
    {
        var state = DescriptionEditing("alpha\nbeta\ngamma");
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var lines = frame.PlainLines();
        var rows = frame.Regions.Where(region => region.Target == MouseTarget.EditorText).ToList();

        Assert.Equal([0, 1, 2], rows.Select(row => row.Line));
        Assert.StartsWith("alpha", lines[rows[0].Top][rows[0].Left..]);
        Assert.StartsWith("beta", lines[rows[1].Top][rows[1].Left..]);
        Assert.StartsWith("gamma", lines[rows[2].Top][rows[2].Left..]);
    }

    [Fact]
    public void GivenALongDescriptionScrolledByItsCaret_WhenClickingAShownRow_ThenTheCaretGoesToThatTextLine()
    {
        var text = LongText();
        var state = DescriptionEditing(text);
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var rows = frame.Regions.Where(region => region.Target == MouseTarget.EditorText).ToList();
        var shown = frame.PlainLines()[rows[2].Top][rows[2].Left..];

        var cell = TextCell(frame, shown[..5], 2);
        var result = RegionProbe.At(state, frame, cell.Column, cell.Row, MouseGesture.Click)!;

        var field = result.State.Session.Tabs[0].Form!.Fields[0];
        Assert.Equal(string.Join('\n', text.Split('\n').Skip(rows[2].Line)).Length - 2, field.Back);
    }

    [Fact]
    public void GivenALongDescription_WhenClickingThenDoubleClickingAtTheSameCell_ThenTheWordOfThatLineIsSelected()
    {
        var state = DescriptionEditing(LongText());
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var rows = frame.Regions.Where(region => region.Target == MouseTarget.EditorText).ToList();
        var word = frame.PlainLines()[rows[1].Top][rows[1].Left..].Trim()[..5];
        var cell = TextCell(frame, word, 1);

        var clicked = RegionProbe.At(state, frame, cell.Column, cell.Row, MouseGesture.Click)!.State;
        var again = FrameBuilder.BuildStyled(clicked, Width, Height, _keymap)!;
        var selected = RegionProbe.At(clicked, again, cell.Column, cell.Row, MouseGesture.DoubleClick)!.State;

        Assert.Equal(word, FormEditor.SelectedText(selected.Session.Tabs[0].Form!.Fields[0]));
    }

    [Fact]
    public void GivenALongDescription_WhenDraggingInSeveralMoves_ThenTheWindowStaysAndTheSelectionFollowsThePointer()
    {
        var state = DescriptionEditing(LongText());
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var rows = frame.Regions.Where(region => region.Target == MouseTarget.EditorText).ToList();
        var from = new ScreenCell(rows[1].Left + 1, rows[1].Top);
        var to = new ScreenCell(rows[4].Left + 3, rows[4].Top);
        var current = state;

        for (var move = 0; move < 3; move++)
        {
            var drawn = FrameBuilder.BuildStyled(current, Width, Height, _keymap)!;
            var context = MouseContext.From(current, Width, Height, FrameBuilder.HeaderRows, _now, drawn.Regions);
            var commands = MouseResolver.ResolveDragMove(context, from.Column, from.Row, to.Column, to.Row, move > 0);

            current = _reducer.Reduce(current, Assert.Single(commands)).State;
        }

        var expected = string.Join('\n', LongText().Split('\n')[(rows[1].Line)..(rows[4].Line + 1)]);
        var selected = FormEditor.SelectedText(current.Session.Tabs[0].Form!.Fields[0]);
        Assert.StartsWith(expected[1..5], selected);
        Assert.Equal(3, selected.Count(static character => character == '\n'));
    }

    private static string LongText()
    {
        return string.Join('\n', Enumerable.Range(1, 30).Select(static number => $"row{number:00}"));
    }

    private static ScreenCell TextCell(StyledFrame frame, string text, int offset)
    {
        var lines = frame.PlainLines();
        var region = frame.Regions
            .Where(candidate => candidate.Target == MouseTarget.EditorText)
            .First(candidate => lines[candidate.Top][candidate.Left..].Contains(text, StringComparison.Ordinal));

        return new ScreenCell(lines[region.Top].IndexOf(text, region.Left, StringComparison.Ordinal) + offset, region.Top);
    }

    private AppState DescriptionEditing(string text)
    {
        var state = Loaded(AdfMarkdownParser.Parse(text));

        for (var step = 0; step < 40 && !OnDescription(state); step++)
        {
            state = Keys(state, "j");
        }

        var opened = _reducer.Reduce(state, new KeyPressed("Enter"));
        var tab = opened.State.Session.Tabs[0];
        var field = FormField.Create(FormFieldIds.Description, "Description", FormFieldKind.Paragraph, false, [])
            .WithInitial(FormValue.OfText(text));

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private static bool OnDescription(AppState state)
    {
        var detail = state.Session.Tabs[0].Detail[^1];

        return detail.Document!.Targets[detail.Cursor] is { Kind: DetailTargetKind.Field } target
            && target.Value == DetailFieldIds.Description;
    }

    private readonly record struct ScreenCell(int Column, int Row);

    private AppState TitleEditing()
    {
        var opened = _reducer.Reduce(Loaded(), new KeyPressed("Enter"));
        var tab = opened.State.Session.Tabs[0];
        var field = FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, [])
            .WithInitial(FormValue.OfText("Fix login"));

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private AppState PriorityDropdown()
    {
        var state = Loaded();
        var tab = state.Session.Tabs[0];

        state = _reducer.Reduce(state, new IssueEditableLoaded(tab.Id, "GCM-12", [DetailFieldIds.Priority])).State;
        state = Keys(state, "Ctrl-l", "j", "Enter");

        var form = state.Session.Tabs[0].Form!;
        var field = FormField.Create(FormFieldIds.Priority, "Priority", FormFieldKind.Select, false, _priorities)
            .WithInitial(FormValue.OfChosen(["2"]));

        state = _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, form.RequestId, [field])).State;

        return Keys(state, "Down");
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key)).State);
    }

    private AppState Loaded(AdfDocument? description = null)
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };

        state = _reducer.Reduce(state, new ViewportChanged(
            Height - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows,
            Width,
            FrameBuilder.BodyHeight(Height))).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "GCM");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var row = new IssueSummary("1", "GCM-12", "Fix login", _status, _type, null, null, [], null, null, "GCM");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = _reducer.Reduce(listed, new KeyPressed("Enter")).State;
        var detail = shown.Session.Tabs[0].Detail[0];

        return _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(description), _now)).State;
    }

    private static IssueDetail Issue(AdfDocument? description)
    {
        return new IssueDetail(
            "1",
            "GCM-12",
            "Fix login",
            _status,
            _type,
            "High",
            new IssueAssignee("j", "Jane Doe"),
            new IssueAssignee("o", "Ola Nordmann"),
            ["auth"],
            _now.AddDays(-9),
            _now.AddHours(-2),
            "GCM",
            description,
            null,
            [new IssueStub("GCM-18", "Migration", _status, _type)],
            [],
            []);
    }
}
