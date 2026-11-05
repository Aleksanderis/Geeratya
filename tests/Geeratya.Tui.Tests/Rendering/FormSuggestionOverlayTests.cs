using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The suggestion popup inside the field form at the three snapshot sizes, with wide-character names, a person
/// field at the bottom of the form, and the loading, failed and empty states. To refresh after an intended change,
/// copy the actual frame from the failure into the matching file and review the diff.
/// </summary>
public class FormSuggestionOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly SuggestionSource _labels = new(SuggestionKind.Labels, null, []);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPopup_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(WithForm(LabelsForm(SuggestionStatus.Loaded)), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var name = $"form-suggest-{width}x{height}.txt";

        Assert.Equal(SnapshotFile.Expect(name, actual), actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPopup_WhenReadingTheRegions_ThenPopupRowsAndFieldRowsAreTagged(int width, int height)
    {
        var state = WithForm(LabelsForm(SuggestionStatus.Loaded));
        var frame = FrameBuilder.BuildStyled(state, width, height, _keymap)!;
        var choices = RegionProbe.Rows(frame, MouseTarget.FormChoice);
        var fields = RegionProbe.Rows(frame, MouseTarget.FormField);

        Assert.NotEmpty(choices);
        Assert.Equal(Enumerable.Range(0, choices.Count), choices.Select(row => row.Index));
        Assert.Contains("日本語チーム", choices[0].Text, StringComparison.Ordinal);
        Assert.NotEmpty(fields);
        Assert.DoesNotContain(fields, field => choices.Any(choice => choice.Region.Top == field.Region.Top));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenClickingASecondRow_ThenTheValueIsTakenLikeEnterOnIt()
    {
        var state = WithForm(LabelsForm(SuggestionStatus.Loaded));
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.FormChoice).Single(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;
        var form = result.State.Session.Tabs[0].Form!;

        Assert.Equal(["backend", "日本チーム-北"], form.Fields[0].Value.Chosen);
        Assert.Null(form.Suggest);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenClickingAnotherField_ThenItIsFocusedAndThePopupCloses()
    {
        var state = WithForm(LabelsForm(SuggestionStatus.Loaded));
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.FormField).First(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;
        var form = result.State.Session.Tabs[0].Form!;

        Assert.Equal(1, form.Focus);
        Assert.Null(form.Suggest);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAPersonFieldLastInTheForm_WhenBuildingTheFrame_ThenThePopupStaysOnScreen(int width, int height)
    {
        var lines = FrameBuilder.Build(WithForm(PersonForm()), width, height, _keymap);

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
        Assert.Contains(lines, line => line.Contains("Jo Smith", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAPopup_WhenBuildingTheFrame_ThenTheCursorRowIsMarkedAndWideNamesAreNotSplit()
    {
        var lines = FrameBuilder.Build(WithForm(LabelsForm(SuggestionStatus.Loaded)), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("日本チーム", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("日本 (new)", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.Equal(80, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenQuickRowsWithNothingHighlighted_WhenBuildingTheFrame_ThenNoRowCarriesThePointer()
    {
        var form = PersonForm();
        var rows = new[] { AssigneeQuickRows.Me("acc-me", "Mia Quick"), AssigneeQuickRows.Unassigned };
        var person = form.Fields[^1] with { Entry = string.Empty };
        var quick = form.Suggest! with { Query = string.Empty, Items = rows, Cursor = -1, RequestId = 0 };

        var fields = form.Fields.Take(form.Fields.Count - 1).Append(person).ToList();
        var shown = form with { Fields = fields, Suggest = quick };

        var lines = FrameBuilder.Build(WithForm(shown), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Me (Mia Quick)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Unassigned", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("> Me (", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALoadingLookup_WhenBuildingTheFrame_ThenItSaysSearching()
    {
        var lines = FrameBuilder.Build(WithForm(LabelsForm(SuggestionStatus.Loading)), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("searching…", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFailedLookup_WhenBuildingTheFrame_ThenItSaysSoAndKeepsTheLoadedRows()
    {
        var form = LabelsForm(SuggestionStatus.Failed, "offline");

        var lines = FrameBuilder.Build(WithForm(form), 100, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("lookup failed", StringComparison.Ordinal) && line.Contains("offline"));
        Assert.Contains(lines, line => line.Contains("日本語チーム", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALoadedEmptyList_WhenBuildingTheFrame_ThenItSaysNoMatch()
    {
        var form = LabelsForm(SuggestionStatus.Loaded);
        var empty = form with { Suggest = form.Suggest! with { Items = [] } };

        var lines = FrameBuilder.Build(WithForm(empty), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("No match", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenBuildingTheFrame_ThenTheFooterNamesTheKeysOfTheList()
    {
        var lines = FrameBuilder.Build(WithForm(LabelsForm(SuggestionStatus.Loaded)), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("choose", StringComparison.Ordinal) && line.Contains("close list"));
    }

    [Fact]
    public void GivenAChosenPerson_WhenTheFieldIsNotFocused_ThenTheNameShowsNotTheAccountId()
    {
        var form = PersonForm();
        var person = form.Fields[^1].WithInitial(FormValue.OfChosen(["acc-1"])) with
        {
            Options = [new FieldOption("acc-1", "Jo Smith")],
        };
        var unfocused = form with { Fields = [form.Fields[0], person], Focus = 0, Suggest = null };

        var lines = FrameBuilder.Build(WithForm(unfocused), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Jo Smith", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("acc-1", StringComparison.Ordinal));
    }

    private static FormState LabelsForm(SuggestionStatus status, string? error = null)
    {
        var labels = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen(["backend"])) with { Entry = "日本" };
        var items = new[]
        {
            Item("日本語チーム"),
            Item("日本チーム-北"),
            new Suggestion("日本", "日本", SuggestionRanker.NewValueDetail, false),
        };
        var popup = new SuggestionState(0, _labels, "日本", items, items, 1, status, 9, error);
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);

        return Base() with { Fields = [labels, summary], Focus = 0, Suggest = popup };
    }

    private static FormState PersonForm()
    {
        var fillers = Enumerable.Range(1, 6)
            .Select(number => FormField.Create($"f{number}", $"Field {number}", FormFieldKind.Text, false, []));
        var person = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, []) with { Entry = "jo" };
        var users = new[] { new Suggestion("acc-1", "Jo Smith", null, false), new Suggestion("acc-2", "Jon Doe", null, true) };
        var source = new SuggestionSource(SuggestionKind.AssignableUsers, "A-1", []);
        var popup = new SuggestionState(6, source, "jo", users, users, 0, SuggestionStatus.Loaded, 4, null);

        return Base() with { Fields = [.. fillers, person], Focus = 6, Suggest = popup };
    }

    private static Suggestion Item(string label)
    {
        return new Suggestion(label, label, null, false);
    }

    private static FormState Base()
    {
        var target = new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null);

        return FormState.Opened(target, 3, "https://work.atlassian.net/browse/A-1") with { IsLoading = false };
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
        var withForm = loaded.Session.Tabs[0] with { Form = form };

        return loaded with { Session = loaded.Session with { Tabs = [withForm] }, Mode = InputMode.Insert };
    }
}
