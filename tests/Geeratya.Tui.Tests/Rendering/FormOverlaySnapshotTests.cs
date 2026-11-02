using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The field form at the three snapshot sizes, with a long wide-character label, a required field left empty, a
/// server error at a field and a field the form cannot edit. To refresh after an intended change, copy the actual
/// frame from the failure into the matching file and review the diff.
/// </summary>
public class FormOverlaySnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenForm_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(), width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"form-{width}x{height}.txt");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void GivenTheLabelsFieldInFocus_WhenBuildingTheFrame_ThenTheLabelsAndTheCaretShow()
    {
        var lines = FrameBuilder.Build(Sample(), 120, 36, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("[backend] [ui]", StringComparison.Ordinal) && line.Contains('_'));
        Assert.Contains(lines, line => line.Contains("> Labels", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenARequiredFieldWithAnError_WhenBuildingTheFrame_ThenTheErrorShowsUnderTheField()
    {
        var lines = FrameBuilder.Build(Sample(), 120, 36, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("! Required", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("(edit in browser)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("net/browse/A-1", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALoadingForm_WhenBuildingTheFrame_ThenItSaysLoading()
    {
        var state = WithForm(FormState.Opened(Target(), 3, null));

        var lines = FrameBuilder.Build(state, 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Loading fields…", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenFocusOnALateField_WhenBuildingASmallFrame_ThenTheFocusedFieldIsScrolledIntoView()
    {
        var form = SampleForm() with { Focus = 3 };

        var lines = FrameBuilder.Build(WithForm(form), 40, 10, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("> ", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.Equal(40, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenManyLongLabelsAndATypedOne_WhenBuildingANarrowFrame_ThenLabelsWrapAndTheCaretStaysVisible()
    {
        var labels = Enumerable.Range(1, 12).Select(number => $"team-label-{number}").ToArray();
        var field = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen(labels)) with { Entry = "typing-now" };
        var form = SampleForm() with { Fields = [field], Focus = 0 };

        var lines = FrameBuilder.Build(WithForm(form), 40, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("[team-label-12]", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("typing-now_", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.Equal(40, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenALongTextValue_WhenFocused_ThenTheEndWithTheCaretShowsAfterAnEllipsis()
    {
        var field = FormField.Create("summary", "Summary", FormFieldKind.Text, false, [])
            .WithInitial(FormValue.OfText(new string('x', 80) + "THE-END"));
        var form = SampleForm() with { Fields = [field], Focus = 0 };

        var lines = FrameBuilder.Build(WithForm(form), 40, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains('…') && line.Contains("THE-END_", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheStatusPicker_WhenBuildingTheFrameAt80x24_ThenTheTitleTheFieldAndTheTransitionsShow()
    {
        var target = new TransitionPickTarget("A-1", [], null, [])
            .With([Transition("21", "In Progress"), Transition("41", "Done")]);
        var form = FormState.Opened(target, 3, null) with { Fields = target.Fields(), IsLoading = false };

        var lines = FrameBuilder.Build(WithForm(form), 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Change status of A-1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("> Move to *", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("( ) In Progress", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("( ) Done", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenACardMoveStillLoading_WhenBuildingTheFrame_ThenNoFormBoxShows()
    {
        var target = new TransitionPickTarget("A-1", ["2"], "In Progress", []);

        var lines = FrameBuilder.Build(WithForm(FormState.Opened(target, 3, null)), 80, 24, _helpLines, _keymap);

        Assert.DoesNotContain(lines, line => line.Contains("Move A-1 to In Progress", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Loading fields", StringComparison.Ordinal) && line.Contains('┌'));
    }

    private static Transition Transition(string id, string name)
    {
        return new Transition(id, name, new IssueStatus(id, name, "indeterminate"), false, true, []);
    }

    private static EditFormTarget Target()
    {
        return new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null);
    }

    private static AppState Sample()
    {
        return WithForm(SampleForm());
    }

    private static FormState SampleForm()
    {
        var labels = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen(["backend", "ui"]));
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, true, []) with { Error = "Required" };
        var priority = FormField.Create(
                "priority",
                "Priority",
                FormFieldKind.Select,
                false,
                [new FieldOption("1", "High"), new FieldOption("2", "Low")])
            .WithInitial(FormValue.OfChosen(["2"]));
        var longLabel = FormField.Create(
            "customfield_1",
            "Eine sehr lange Bezeichnung für 日本語 das Feld Schätzung in Stunden pro Person",
            FormFieldKind.Number,
            false,
            []);
        var team = FormField.Create("customfield_2", "Team", FormFieldKind.Unsupported, false, []);

        return FormState.Opened(Target(), 3, "https://work.atlassian.net/browse/A-1") with
        {
            Fields = [labels, summary, priority, longLabel, team],
            IsLoading = false,
        };
    }

    private static AppState WithForm(FormState form)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var reducer = new AppReducer(_keymap);
        var profiles = ProfileCatalog.Empty.With(profile);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profile) };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var row = new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            ["backend"],
            null,
            null,
            "A");
        var page = new IssuePage([row], null, null);
        var loaded = reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;

        return ViewReducerReplace(loaded, form) with { Mode = InputMode.Insert };
    }

    private static AppState ViewReducerReplace(AppState state, FormState form)
    {
        var tab = state.Session.Tabs[0] with { Form = form };

        return state with { Session = state.Session with { Tabs = [tab] } };
    }
}
