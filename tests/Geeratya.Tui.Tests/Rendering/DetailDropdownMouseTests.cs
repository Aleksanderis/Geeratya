using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
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

    private AppState Loaded()
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
            [new IssueStub("GCM-18", "Migration", _status, _type)],
            [],
            []);
    }
}
