using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>
/// Summary and Description edited in place in the issue detail (#709): the edit state of the title and Description
/// panes, their own writes, the discard question, a description that stays read-only and the document the panes draw.
/// </summary>
[Trait("Os", "Independent")]
public class InPaneEditTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData("Enter")]
    [InlineData("i")]
    public void GivenTheCursorOnTheTitlePane_WhenPressingEnterOrI_ThenTheTitleEditStateOpens(string key)
    {
        var result = Press(OnTarget(Opened(null), DetailFieldIds.Summary), key);

        var form = Active(result.State).Form!;
        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(PaneIds.Title, form.Pane);
        Assert.Null(form.Dropdown);
        Assert.Equal(FormFieldIds.Summary, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData("i")]
    public void GivenTheCursorOnTheDescriptionPane_WhenPressingEnterOrI_ThenTheDescriptionEditStateOpens(string key)
    {
        var result = Press(OnTarget(Opened(Described("Hello")), DetailFieldIds.Description), key);

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(PaneIds.Description, Active(result.State).Form!.Pane);
        Assert.Equal(FormFieldIds.Description, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
    }

    [Fact]
    public void GivenTheCursorOnAnotherField_WhenPressingI_ThenNothingOpensAndTheStatusSaysWhatIEdits()
    {
        var onSide = Press(Opened(null), "Ctrl-l").State;

        var result = Press(onSide, "i");

        Assert.Null(Active(result.State).Form);
        Assert.Empty(result.Effects);
        Assert.Contains("title or the Description", result.State.Message);
    }

    [Fact]
    public void GivenADescriptionThatDoesNotRoundTrip_WhenEditing_ThenItStaysReadOnlyAndSaysToUseTheBrowser()
    {
        var table = new AdfDocument(1, [new AdfParagraph([new AdfText("cut short", [])])], true);
        var state = OnTarget(Opened(table), DetailFieldIds.Description);

        var result = Press(state, "Enter");

        Assert.Null(Active(result.State).Form);
        Assert.Empty(result.Effects);
        Assert.Equal(FormOpener.DescriptionBrowserText, result.State.Message);
        Assert.Contains("(edit in browser)", Plain(Document(result.State)));
    }

    [Fact]
    public void GivenADescriptionThatRoundTrips_WhenBuildingTheDocument_ThenThePaneHasNoBrowserNote()
    {
        var state = Opened(Described("Plain text"));

        Assert.DoesNotContain("(edit in browser)", Plain(Document(state)));
    }

    [Fact]
    public void GivenTheSummaryEdited_WhenPressingCtrlS_ThenOnlyTheSummaryIsWritten()
    {
        var state = Loaded(Press(OnTarget(Opened(Described("Body")), DetailFieldIds.Summary), "Enter"), SummaryField());

        var saved = Press(Press(state, "!").State, "Ctrl-s");

        var request = Assert.IsType<EditRequest>(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saved.Effects)).Request);
        var change = Assert.Single(request.Changes);
        Assert.Equal(FormFieldIds.Summary, change.FieldId);
        Assert.Equal("Issue 1!", change.Value.Text);
        Assert.True(Active(saved.State).Form!.IsSaving);
    }

    [Fact]
    public void GivenTheSummaryEdited_WhenPressingEnter_ThenItSavesBecauseTheTitleIsOneLine()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "i"), SummaryField());

        var saved = Press(Press(state, "x").State, "Enter");

        Assert.IsType<EditRequest>(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saved.Effects)).Request);
    }

    [Fact]
    public void GivenTheDescriptionEdited_WhenPressingEnterThenCtrlS_ThenItAddsALineAndWritesOnlyTheDescription()
    {
        var state = Loaded(
            Press(OnTarget(Opened(Described("Body")), DetailFieldIds.Description), "Enter"),
            DescriptionField("Body"));

        var lined = Press(state, "Enter", "x");
        var saved = Press(lined.State, "Ctrl-s");

        Assert.Empty(lined.Effects);

        var request = Assert.IsType<EditRequest>(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saved.Effects)).Request);
        var change = Assert.Single(request.Changes);
        Assert.Equal(FormFieldIds.Description, change.FieldId);
        Assert.Equal("Body\nx", change.Value.Text);
    }

    [Fact]
    public void GivenChangesInThePane_WhenPressingEscape_ThenItAsksBeforeDiscardingAndYDropsThem()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());
        var typed = Press(state, "z").State;

        var asked = Press(typed, "Esc");
        var discarded = Press(asked.State, "y");

        Assert.True(Active(asked.State).Form!.DiscardPending);
        Assert.Contains("Discard the changes?", Plain(Build(asked.State, 100, 30)));
        Assert.Null(Active(discarded.State).Form);
        Assert.Empty(discarded.Effects);
        Assert.Equal("Issue 1", Active(discarded.State).Detail[0].Issue!.Summary);
    }

    [Fact]
    public void GivenNoChangesInThePane_WhenPressingEscape_ThenTheEditStateLeavesAtOnce()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());

        var left = Press(state, "Esc");

        Assert.Null(Active(left.State).Form);
        Assert.Single(Active(left.State).Detail);
    }

    [Fact]
    public void GivenTheTitleEditState_WhenBuildingTheDocument_ThenTheTitlePaneShowsTheCaretAndTheEditingChip()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());

        var text = Plain(Build(state, 100, 30));

        Assert.Contains("Issue 1", text);
        Assert.Contains("editing", text);
    }

    [Fact]
    public void GivenALongDescriptionInTheEditState_WhenBuildingTheDocument_ThenAWindowKeepsTheCaretRowInView()
    {
        var text = string.Join('\n', Enumerable.Range(1, 30).Select(number => $"line {number}"));
        var state = Loaded(
            Press(OnTarget(Opened(Described("x")), DetailFieldIds.Description), "Enter"),
            DescriptionField(text));

        var document = Build(state, 100, 30);
        var plain = Plain(document);

        Assert.Contains("line 30", plain);
        Assert.DoesNotContain("line 1 ", plain);
        Assert.Contains(document.PaneScrolls, scroll => scroll is not null);
        Assert.Contains("24-30 of 30", plain);
    }

    [Fact]
    public void GivenTheCaretInTheMiddleOfALongDescription_WhenBuildingTheDocument_ThenRowsAboveAndBelowShow()
    {
        var text = string.Join('\n', Enumerable.Range(1, 30).Select(number => $"line {number}"));
        var field = DescriptionField(text) with { Back = text.Length - text.IndexOf("line 15", StringComparison.Ordinal) };
        var state = Loaded(Press(OnTarget(Opened(Described("x")), DetailFieldIds.Description), "Enter"), field);

        var plain = Plain(Build(state, 100, 30));

        Assert.Contains("line 12", plain);
        Assert.Contains("line 15", plain);
        Assert.Contains("line 18", plain);
        Assert.DoesNotContain("line 19", plain);
        Assert.Contains("12-18 of 30", plain);
    }

    [Fact]
    public void GivenTheDescriptionEditState_WhenBuildingTheDocument_ThenItIsAFilledFieldWithABlockCaretAndTheKeyHint()
    {
        var state = Loaded(
            Press(OnTarget(Opened(Described("x")), DetailFieldIds.Description), "Enter"),
            DescriptionField("abc"));

        var document = Build(state, 100, 30);
        var spans = document.Lines.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.EditField && span.Text.StartsWith("abc", StringComparison.Ordinal));
        Assert.Contains(spans, span => span.Role == TextRole.EditCaret && span.Text == " ");
        Assert.Contains("Ctrl-s save   Esc cancel", Plain(document));
    }

    [Fact]
    public void GivenTheDiscardQuestion_WhenBuildingTheDocument_ThenTheHintShowsTheKeysOfTheQuestion()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());
        var asked = Press(Press(state, "z").State, "Esc").State;

        var plain = Plain(Build(asked, 100, 30));

        Assert.Contains("y discard   n/Esc keep editing", plain);
        Assert.DoesNotContain("Ctrl-s save", plain);
    }

    [Fact]
    public void GivenTheCaretMovingThroughTheText_WhenBuildingTheDocument_ThenTheRowKeepsItsTextAndWidth()
    {
        var state = Loaded(
            Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"),
            SummaryField());
        var atEnd = CaretRow(Build(state, 60, 30));
        var moved = CaretRow(Build(Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField() with { Back = 3 }), 60, 30));

        Assert.Equal(atEnd.Sum(span => DisplayWidth.Of(span.Text)), moved.Sum(span => DisplayWidth.Of(span.Text)));
        Assert.Equal(string.Concat(atEnd.Select(span => span.Text)).TrimEnd(), string.Concat(moved.Select(span => span.Text)).TrimEnd());
        Assert.Single(moved, span => span.Role == TextRole.EditCaret);
        Assert.Equal(1, DisplayWidth.Of(Assert.Single(moved, span => span.Role == TextRole.EditCaret).Text));
        Assert.NotEqual(" ", Assert.Single(moved, span => span.Role == TextRole.EditCaret).Text);
    }

    private static IReadOnlyList<StyledSpan> CaretRow(DetailDocument document)
    {
        return document.Lines.First(line => line.Spans.Any(span => span.Role == TextRole.EditCaret)).Spans;
    }

    [Fact]
    public void GivenTheTitleEditState_WhenBuildingTheDocument_ThenTheRowIsFilledToThePaneWidth()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());

        var document = Build(state, 60, 30);
        var row = document.Lines.First(line => line.Spans.Any(span => span.Role == TextRole.EditCaret));
        var fieldColumns = row.Spans.Where(span => span.Role is TextRole.EditField or TextRole.EditCaret).Sum(span => DisplayWidth.Of(span.Text));

        Assert.True(fieldColumns >= 50, $"field is {fieldColumns} columns");
    }

    [Fact]
    public void GivenAShortScreen_WhenEditingTheDescription_ThenTheEditorShrinksToTheRowsThatFit()
    {
        var text = string.Join('\n', Enumerable.Range(1, 30).Select(number => $"line {number}"));
        var state = Loaded(
            Press(OnTarget(Opened(Described("x")), DetailFieldIds.Description), "Enter"),
            DescriptionField(text));

        var document = Build(state, 100, 6);

        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Description);
        var start = document.PaneStarts[pane];
        var end = document.PaneStarts[pane + 1];
        Assert.Contains("line 30", Plain(document));
        Assert.True(end - start <= 6, $"editor is {end - start} lines");
    }

    [Fact]
    public void GivenAZoomedDescription_WhenEditing_ThenTheEditorTakesTheWholeHeight()
    {
        var text = string.Join('\n', Enumerable.Range(1, 40).Select(number => $"line {number}"));
        var zoomed = Press(OnTarget(Opened(Described("x")), DetailFieldIds.Description), "Space", "z");
        var state = Loaded(Press(zoomed.State, "Enter"), DescriptionField(text));

        var document = Build(state, 100, 30);

        Assert.Equal([PaneIds.Description], document.SectionIds);
        Assert.True(document.Lines.Count >= 29);
    }

    [Fact]
    public void GivenARefusedSave_WhenTheWriteFails_ThenThePaneKeepsTheTextAndShowsTheError()
    {
        var state = Loaded(Press(OnTarget(Opened(null), DetailFieldIds.Summary), "Enter"), SummaryField());
        var saved = Press(Press(state, "!").State, "Ctrl-s");
        var id = saved.State.Session.Operations[0].Id;
        var denied = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);

        var failed = _reducer.Reduce(saved.State, new WriteFailed(id, _now, denied));

        var form = Active(failed.State).Form!;
        Assert.False(form.IsSaving);
        Assert.Equal("Issue 1!", form.Fields[0].Value.Text);
        Assert.Contains("No permission", Plain(Build(failed.State, 100, 30)));
    }

    private static DetailDocument Build(AppState state, int width, int rows)
    {
        var tab = Active(state);

        return IssueDocumentBuilder.Build(tab.Detail[^1], width, rows, null, tab.Form);
    }

    private static string Plain(DetailDocument document)
    {
        return string.Join('\n', document.Lines.Select(static line => line.PlainText));
    }

    private static DetailDocument Document(AppState state)
    {
        return Active(state).Detail[^1].Document!;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.First(tab => tab.Id == state.Session.Active);
    }

    private static FormField SummaryField()
    {
        return FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, [])
            .WithInitial(FormValue.OfText("Issue 1"));
    }

    private static FormField DescriptionField(string text)
    {
        return FormField.Create(FormFieldIds.Description, "Description", FormFieldKind.Paragraph, false, [])
            .WithInitial(FormValue.OfText(text));
    }

    private static AdfDocument Described(string text)
    {
        return AdfMarkdownParser.Parse(text);
    }

    private AppState Loaded(ReduceResult opened, FormField field)
    {
        var tab = Active(opened.State);

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key, _now));
        }

        return result;
    }

    private AppState OnTarget(AppState state, string fieldId)
    {
        var steps = 0;

        while (!IsField(state, fieldId) && steps++ < Document(state).Lines.Count)
        {
            state = Press(state, "j").State;
        }

        Assert.True(IsField(state, fieldId), $"no field {fieldId}");

        return state;
    }

    private static bool IsField(AppState state, string fieldId)
    {
        var target = Document(state).Targets[Active(state).Detail[^1].Cursor];

        return target is { Kind: DetailTargetKind.Field } && target.Value == fieldId;
    }

    private AppState Opened(AdfDocument? description)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 100)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];

        return _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(description), _now)).State;
    }

    private static IssueDetail Issue(AdfDocument? description)
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            new IssueAssignee("acc-eve", "Eve Example"),
            new IssueAssignee("acc-ola", "Ola Nordmann"),
            ["backend"],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            description,
            null,
            [],
            [],
            []);
    }
}
