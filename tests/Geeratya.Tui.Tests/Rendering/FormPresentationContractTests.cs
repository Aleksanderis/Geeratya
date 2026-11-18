using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One scenario set over every presentation of a form (<c>docs/internal/ui-consistency.md</c>, component 3): the
/// field box, the create panes, the dropdown under a Details field and the in-place edit all draw a field through
/// <see cref="FieldRows"/> and behave through <see cref="FormShell"/>, so the same typing, error, discard and
/// "nothing changed" scenarios hold in each, and none of them breaks on a tiny terminal. A new presentation is added to
/// <see cref="Presentations"/>.
/// </summary>
[Trait("Os", "Independent")]
public class FormPresentationContractTests
{
    private const string LongError =
        "The refusal is Jira's own sentence and it must stay readable in every presentation until its last word";

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);

    private readonly AppReducer _reducer = new(_keymap);

    public static TheoryData<string> Presentations()
    {
        return ["box", "create", "dropdown", "in-place"];
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenTheFieldIsEdited_WhenATextIsTyped_ThenItShowsInTheValueRoleWithTheCaretAfterIt(string presentation)
    {
        var state = Keys(Open(presentation, Field("hello")), "x");

        var spans = Spans(Frame(state, 120, 36));

        Assert.Contains(spans, span => span.Text.Contains("hellox", StringComparison.Ordinal) && IsValueRole(span.Role));
        Assert.Contains(spans, span => span.Role == TextRole.EditCaret);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAFieldWithAnError_WhenDrawn_ThenItWrapsUnderTheFieldInTheErrorRoleAndIsNeverCut(string presentation)
    {
        var state = Open(presentation, Field("hello") with { Error = LongError });

        var spans = Spans(Frame(state, 120, 36)).Where(span => span.Role == TextRole.Error).ToList();

        var text = string.Join(' ', spans.Select(static span => span.Text.Trim()));
        Assert.Contains("! The refusal", text, StringComparison.Ordinal);
        Assert.Contains("last word", text, StringComparison.Ordinal);
        Assert.DoesNotContain("…", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAChangedField_WhenEscIsPressed_ThenOneDiscardConfirmIsAskedAndTheFormStaysDrawnUnderIt(
        string presentation)
    {
        var state = Keys(Open(presentation, Field("hello")), "x");

        var asked = Asked(state);
        var frame = Frame(asked, 120, 36);

        Assert.Equal(InputMode.Confirm, asked.Mode);
        Assert.True(DiscardPrompts.Is(asked.Confirm));
        Assert.NotNull(asked.Session.Tabs[0].Form);
        Assert.Contains(frame.Body, line => line.PlainText.Contains("Discard", StringComparison.Ordinal));
        Assert.Contains(frame.Body, line => line.PlainText.Contains("hellox", StringComparison.Ordinal));
        Assert.Contains("CONFIRM", frame.StatusLine);
        Assert.Contains("y/n", frame.StatusLine);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenTheDiscardQuestion_WhenAnswered_ThenYClosesTheFormAndNQOrEscKeepItsText(string presentation)
    {
        var asked = Asked(Keys(Open(presentation, Field("hello")), "x"));

        var discarded = Keys(asked, "y");

        Assert.Null(discarded.Session.Tabs[0].Form);
        Assert.Null(discarded.Confirm);

        foreach (var key in new[] { "n", "q", "Esc" })
        {
            var kept = Keys(asked, key);

            Assert.Null(kept.Confirm);
            Assert.Equal(InputMode.Insert, kept.Mode);
            Assert.Contains("hellox", kept.Session.Tabs[0].Form!.Fields.Single(Edited).Value.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenNothingChanged_WhenTheFormIsSaved_ThenItClosesSayingNothingChanged(string presentation)
    {
        var saved = Keys(Open(presentation, Field("hello")), "Ctrl-s");

        Assert.Null(saved.Session.Tabs[0].Form);
        Assert.Equal(FormShell.NothingChangedText, saved.Message);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnyTerminalSize_WhenTheFormIsDrawn_ThenItNeverThrowsAndFitsTheBody(string presentation)
    {
        var state = Keys(Open(presentation, Field("hello") with { Error = LongError }), "x");

        foreach (var (width, height) in new[] { (120, 36), (80, 24), (40, 12), (24, 8), (12, 5), (4, 3) })
        {
            var frame = FrameBuilder.BuildStyled(state, width, height, _keymap);

            Assert.True(frame is null || frame.Body.Count <= Math.Max(1, FrameBuilder.BodyHeight(height)));
        }
    }

    [Theory]
    [InlineData("box")]
    [InlineData("create")]
    public void GivenANavigableForm_WhenItIsEditedAndThenSteppedBack_ThenTheEscWordAndTheEnterEditHintAgree(
        string presentation)
    {
        var editing = Frame(Open(presentation, Field("hello")), 140, 36);
        var navigating = Frame(Keys(Open(presentation, Field("hello")), "Esc"), 140, 36);

        Assert.Contains("Esc stop editing", editing.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Tab/Shift-Tab field", editing.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Esc cancel", navigating.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Enter edit", navigating.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Tab/Shift-Tab field", navigating.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheDropdownAndTheInPlaceEdit_WhenDrawn_ThenTheStatusLineNamesTheirKeys()
    {
        var choice = FormField.Create("priority", "Priority", FormFieldKind.Select, true, [new("1", "High")]);
        var dropdown = Frame(Open("dropdown", choice), 140, 36);
        var typed = Frame(Open("dropdown", Field("hello")), 140, 36);
        var inPlace = Frame(Open("in-place", Field("hello")), 140, 36);

        Assert.Contains("Enter choose", dropdown.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Esc close", dropdown.StatusLine, StringComparison.Ordinal);

        // A typed field in the dropdown (the Estimate) has no row to choose: Enter saves it.
        Assert.Contains("Enter save", typed.StatusLine, StringComparison.Ordinal);
        Assert.DoesNotContain("choose", typed.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Esc close", typed.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Ctrl-s save", inPlace.StatusLine, StringComparison.Ordinal);
        Assert.Contains("Esc cancel", inPlace.StatusLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(140)]
    [InlineData(100)]
    [InlineData(80)]
    public void GivenAnOpenPopup_WhenTheStatusLineIsNarrow_ThenEscStaysAndTheOtherKeysDropFromTheEnd(int width)
    {
        var choice = FormField.Create(
            "customfield_pick",
            "Pick",
            FormFieldKind.Select,
            false,
            [new FieldOption("low", "Low"), new FieldOption("high", "High")]);
        var state = Keys(Open("create", choice), "L");

        var line = Frame(state, width, 36).StatusLine;

        Assert.NotNull(state.Session.Tabs[0].Form!.Suggest);
        Assert.Contains("Esc", line, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheBoxForm_WhenItIsEdited_ThenItsFooterUsesTheWordsOfTheStatusLine()
    {
        var editing = Frame(Open("box", Field("hello")), 120, 36);
        var navigating = Frame(Keys(Open("box", Field("hello")), "Esc"), 120, 36);

        Assert.Contains(editing.Body, line => line.PlainText.Contains("Esc stop editing", StringComparison.Ordinal));
        Assert.Contains(navigating.Body, line => line.PlainText.Contains("Esc cancel", StringComparison.Ordinal));
        Assert.DoesNotContain(navigating.Body, line => line.PlainText.Contains("Esc close", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFieldOpenedByCommand_WhenItsDropdownOpens_ThenTheCursorIsOnThatFieldLikeEnterOnIt()
    {
        var state = Loaded();
        var tab = state.Session.Tabs[0];

        state = _reducer.Reduce(state, new IssueEditableLoaded(tab.Id, "GCM-12", [DetailFieldIds.Priority])).State;
        var opened = Keys(state, ":", "p", "r", "i", "o", "r", "i", "t", "y", "Enter");

        var top = opened.Session.Tabs[0].Detail[^1];
        Assert.NotNull(top.SideCursor);
        Assert.Equal(DetailFieldIds.Priority, top.Document!.Side!.Targets[top.SideCursor.Value]!.Value);
        Assert.Equal(DetailFieldIds.Priority, opened.Session.Tabs[0].Form!.Dropdown);
    }

    private static bool IsValueRole(TextRole role)
    {
        return role is TextRole.Editable or TextRole.Body or TextRole.ValueBlock or TextRole.EditField or TextRole.Selected;
    }

    private static bool Edited(FormField field)
    {
        return field.Value.Text.StartsWith("hello", StringComparison.Ordinal);
    }

    private static FormField Field(string text)
    {
        return FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, [])
            .WithInitial(FormValue.OfText(text));
    }

    private static StyledFrame Frame(AppState state, int width, int height)
    {
        return FrameBuilder.BuildStyled(state, width, height, _keymap)!;
    }

    private static List<StyledSpan> Spans(StyledFrame frame)
    {
        return [.. frame.Body.SelectMany(static line => line.Spans)];
    }

    // Esc until the question is asked (a navigable form steps to its navigate state first).
    private AppState Asked(AppState state)
    {
        for (var step = 0; step < 4 && state.Mode != InputMode.Confirm; step++)
        {
            state = Keys(state, "Esc");
        }

        return state;
    }

    private AppState Open(string presentation, FormField field)
    {
        return presentation switch
        {
            "box" => OpenBox(field),
            "create" => OpenCreate(field),
            "dropdown" => OpenDropdown(field),
            _ => OpenInPlace(field with { Id = FormFieldIds.Summary, Label = "Summary" }),
        };
    }

    private static AppState OpenBox(FormField field)
    {
        var other = FormField.Create("other", "Other", FormFieldKind.Text, false, []);
        var form = FormState.Opened(new AssignFormTarget("A-1", null), 3, null) with
        {
            Fields = [field, other],
            IsLoading = false,
            Navigable = true,
        };

        return FormOverlaySnapshotTests.WithForm(form);
    }

    private static AppState OpenCreate(FormField field)
    {
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var target = new CreateFormTarget("A", "A-1", false, new CreateIssueType("10", "Task", string.Empty, 0, false));
        var form = FormState.Opened(target, 3, null) with
        {
            Fields = [summary, field],
            IsLoading = false,
            Navigable = true,
            Focus = 1,
        };

        return CreatePaneOverlaySnapshotTests.Open(form);
    }

    private AppState OpenDropdown(FormField field)
    {
        var state = Loaded();
        var tab = state.Session.Tabs[0];

        state = _reducer.Reduce(state, new IssueEditableLoaded(tab.Id, "GCM-12", [DetailFieldIds.Priority])).State;
        state = Keys(state, "Ctrl-l", "j", "Enter");

        var form = state.Session.Tabs[0].Form!;

        return _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, form.RequestId, [field])).State;
    }

    private AppState OpenInPlace(FormField field)
    {
        var opened = _reducer.Reduce(Loaded(), new KeyPressed("Enter"));
        var tab = opened.State.Session.Tabs[0];

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }

    private AppState Loaded()
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };

        state = _reducer.Reduce(state, new ViewportChanged(
            36 - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows,
            120,
            FrameBuilder.BodyHeight(36))).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "GCM");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var row = new IssueSummary("1", "GCM-12", "Fix login", _status, _type, null, null, [], null, null, "GCM");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = _reducer.Reduce(listed, new KeyPressed("Enter")).State;
        var detail = shown.Session.Tabs[0].Detail[0];

        return _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now)).State;
    }

    private static IssueDetail Issue()
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
            null,
            null,
            [],
            [],
            []);
    }
}
