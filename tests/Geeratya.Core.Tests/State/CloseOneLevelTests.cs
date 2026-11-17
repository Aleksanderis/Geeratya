using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Flash;
using Geeratya.Core.Forms;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.State;

/// <summary>
/// The table behind "Esc and q close one level at a time" (<c>docs/internal/ui-consistency.md</c>, decision 2): one
/// row per surface of the audit, each asserting what Esc and what q do there, and that only the view root quits.
/// </summary>
[Trait("Os", "Independent")]
public sealed class CloseOneLevelTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly PaneGrid _grid = new(
    [
        new PaneSlot("title", 0, 0),
        new PaneSlot("description", 0, 1),
        new PaneSlot("details", 1, 0, 1, 2),
    ]);

    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    public static TheoryData<string> LevelSurfaces()
    {
        return
        [
            "issue", "zoom", "visual", "rank-pickup", "settings", "settings-choice", "profile-manager",
            "manager-from-settings", "context-menu", "confirm", "form-navigate", "form-type-chooser",
            "form-discard-prompt", "messages", "messages-with-settings",
        ];
    }

    public static TheoryData<string> TypedSurfaces()
    {
        return ["picker", "jump", "flash", "palette"];
    }

    [Fact]
    public void GivenTheViewRoot_WhenPressingEscAndQ_ThenEscDoesNothingAndOnlyQQuits()
    {
        var root = Tabbed();

        var esc = Press(root, "Esc");
        var quit = Press(root, "q");

        Assert.Empty(esc.Effects);
        Assert.Equal(InputMode.Normal, esc.State.Mode);
        Assert.Single(quit.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenANoticeAtTheViewRoot_WhenPressingQ_ThenItQuitsAndEscOnlyEndsTheNotice()
    {
        var noticed = Tabbed() with { Message = "Copied A-1" };

        var esc = Press(noticed, "Esc");
        var quit = Press(noticed, "q");

        Assert.Null(esc.State.Message);
        Assert.Empty(esc.Effects);
        Assert.Single(quit.Effects.OfType<QuitEffect>());
    }

    [Theory]
    [MemberData(nameof(LevelSurfaces))]
    public void GivenALevel_WhenPressingEsc_ThenOnlyThatLevelClosesAndTheAppDoesNotQuit(string surface)
    {
        var start = Build(surface);

        var result = Press(start, "Esc");

        Assert.Empty(result.Effects.OfType<QuitEffect>());
        Assert.True(Closed(surface, start, result.State), $"Esc did not close {surface}");
    }

    [Theory]
    [MemberData(nameof(LevelSurfaces))]
    public void GivenALevel_WhenPressingQ_ThenItClosesTheSameLevelAsEscAndTheAppDoesNotQuit(string surface)
    {
        var start = Build(surface);

        Assert.True(surface != "zoom" || PaneActions.IsZoomed(start), "The zoom surface starts zoomed");
        var viaEsc = Press(start, "Esc");
        var viaQ = Press(start, "q");

        Assert.Empty(viaQ.Effects.OfType<QuitEffect>());
        Assert.Equal(viaEsc.State.Mode, viaQ.State.Mode);
        Assert.True(Closed(surface, start, viaQ.State), $"q did not close {surface}");
    }

    [Theory]
    [MemberData(nameof(TypedSurfaces))]
    public void GivenTypedText_WhenPressingEsc_ThenTheFirstEscClearsItAndTheNextCloses(string surface)
    {
        var start = Build(surface);

        var first = Press(start, "Esc");
        var second = Press(first.State, "Esc");

        Assert.Equal(start.Mode, first.State.Mode);
        Assert.Equal(string.Empty, TypedText(first.State));
        Assert.Equal(InputMode.Normal, second.State.Mode);
        Assert.Null(second.State.Picker);
        Assert.Null(second.State.Jump);
        Assert.Null(second.State.FlashJump);
        Assert.Empty(second.Effects);
    }

    [Theory]
    [MemberData(nameof(TypedSurfaces))]
    public void GivenAFilterOrPrompt_WhenPressingQ_ThenItIsTextAndNothingCloses(string surface)
    {
        var start = Build(surface);

        var result = Press(start, "q");

        Assert.Equal(start.Mode, result.State.Mode);
        Assert.EndsWith("q", TypedText(result.State), StringComparison.Ordinal);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheProfileFormWithChanges_WhenPressingEsc_ThenItAsksAndNReturnsToTheForm()
    {
        var form = Build("profile-form");

        var asked = Press(form, "Esc");
        var kept = Press(asked.State, "n");
        var discarded = Press(Press(kept.State, "Esc").State, "y");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Equal(ProfileReducer.DiscardAction, asked.State.Confirm!.ActionOnConfirm);
        Assert.NotNull(asked.State.Form);
        Assert.Equal(InputMode.Insert, kept.State.Mode);
        Assert.Equal("wor", kept.State.Form!.Name);
        Assert.Null(discarded.State.Form);
        Assert.Equal(InputMode.Normal, discarded.State.Mode);
    }

    [Fact]
    public void GivenTheProfileFormWithoutChanges_WhenPressingEsc_ThenItClosesAtOnce()
    {
        var opened = Palette(Tabbed(), "profile add");

        var result = Press(opened, "Esc");

        Assert.Null(result.State.Form);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenTheProfileFormTypingQ_WhenPressing_ThenItIsText()
    {
        var form = Build("profile-form");

        var result = Press(form, "q");

        Assert.Equal("worq", result.State.Form!.Name);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenTheManagerOpenedFromSettings_WhenClosingIt_ThenSettingsReturnsWithItsRow()
    {
        var manager = Build("manager-from-settings");

        var closed = Press(manager, "Esc");

        Assert.Null(closed.State.ProfileList);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
        Assert.Equal(manager.ProfileList!.ReturnTo!.Selected, closed.State.SettingsScreen!.Selected);
    }

    [Fact]
    public void GivenVisualModeInAZoomedPane_WhenPressingEsc_ThenVisualClosesFirstAndZoomStays()
    {
        var zoomed = Press(Build("zoom"), "v").State;

        var first = Press(zoomed, "Esc");
        var second = Press(first.State, "Esc");

        Assert.NotNull(zoomed.Session.Tabs[0].Detail[^1].YankAnchor);
        Assert.Null(first.State.Session.Tabs[0].Detail[^1].YankAnchor);
        Assert.True(PaneActions.IsZoomed(first.State));
        Assert.False(PaneActions.IsZoomed(second.State));
        Assert.NotEmpty(second.State.Session.Tabs[0].Detail);
        Assert.Empty(second.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenAProfileFormWithChangesInTheManagerFromSettings_WhenEscaping_ThenItAsksThenReturnsStepByStep()
    {
        var manager = Build("manager-from-settings");
        var form = Press(manager, "Ctrl-a", "w", "o").State;

        var asked = Press(form, "Esc");
        var discarded = Press(asked.State, "y");
        var closedManager = Press(discarded.State, "Esc");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Null(discarded.State.Form);
        Assert.NotNull(discarded.State.ProfileList);
        Assert.Null(closedManager.State.ProfileList);
        Assert.NotNull(closedManager.State.SettingsScreen);
        Assert.Empty(closedManager.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenAnOpenIssue_WhenPressingQTwice_ThenTheFirstPopsTheIssueAndTheSecondQuits()
    {
        var detail = Press(Tabbed(), "Enter").State;

        var popped = Press(detail, "q");
        var quit = Press(popped.State, "q");

        Assert.Empty(popped.Effects.OfType<QuitEffect>());
        Assert.Empty(popped.State.Session.Tabs[0].Detail);
        Assert.Single(quit.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheTransitionFieldsEditedWithoutChanges_WhenPressingEsc_ThenTheStatusPickerReturnsAndNothingQuits()
    {
        var fields = TransitionFields();

        var esc = Press(fields, "Esc");

        Assert.IsType<TransitionFormTarget>(fields.Session.Tabs[0].Form!.Target);
        Assert.IsType<TransitionPickTarget>(esc.State.Session.Tabs[0].Form!.Target);
        Assert.Equal(InputMode.Insert, esc.State.Mode);
        Assert.Empty(esc.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheTransitionFieldsInTheNavigateState_WhenPressingQ_ThenTheStatusPickerReturns()
    {
        var navigating = WithTab(
            TransitionFields(),
            tab => tab with { Form = tab.Form! with { Navigable = true, Navigating = true } });

        var result = Press(navigating, "q");

        Assert.IsType<TransitionPickTarget>(result.State.Session.Tabs[0].Form!.Target);
        Assert.Empty(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheTransitionFieldsBeingEdited_WhenPressingQ_ThenItIsTextAndTheFormStays()
    {
        var fields = TransitionFields();

        var result = Press(fields, "q");

        Assert.IsType<TransitionFormTarget>(result.State.Session.Tabs[0].Form!.Target);
        Assert.Empty(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheMessagesOverlay_WhenPressingEscThenQ_ThenEachClosesItAndOnlyQAtTheRootQuits()
    {
        var open = StatusMessages.OpenHistory(Tabbed());

        var closed = Press(open, "Esc");
        var quit = Press(closed.State, "q");

        Assert.False(closed.State.MessagesOpen);
        Assert.Empty(closed.Effects);
        Assert.Single(quit.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheCommentEditorWithAPopup_WhenPressingEsc_ThenOnlyThePopupClosesAndQIsText()
    {
        var editing = EditorWithPopup();

        var typed = Press(editing, "q");
        var esc = Press(editing, "Esc");

        Assert.NotNull(editing.Editor!.Suggest);
        Assert.EndsWith("oq", typed.State.Editor!.Buffer.Text, StringComparison.Ordinal);
        Assert.Empty(typed.Effects.OfType<QuitEffect>());
        Assert.NotNull(esc.State.Editor);
        Assert.Null(esc.State.Editor.Suggest);
        Assert.Equal(InputMode.Insert, esc.State.Mode);
    }

    [Fact]
    public void GivenTheFormWithAFilteredPopup_WhenPressingEsc_ThenTheFilterClearsThenTheFormLeavesAndQIsText()
    {
        var form = TransitionFields();
        var tab = form.Session.Tabs[0];
        var popup = tab.Form!.Suggest! with { Query = "do" };
        var open = WithTab(form, _ => tab with { Form = tab.Form with { Suggest = popup } });

        var cleared = Press(open, "Esc");
        var left = Press(cleared.State, "Esc");
        var typed = Press(open, "q");

        Assert.Equal(string.Empty, cleared.State.Session.Tabs[0].Form!.Suggest!.Query);
        Assert.IsType<TransitionFormTarget>(cleared.State.Session.Tabs[0].Form!.Target);
        Assert.IsType<TransitionPickTarget>(left.State.Session.Tabs[0].Form!.Target);
        Assert.Empty(typed.Effects.OfType<QuitEffect>());
        Assert.IsType<TransitionFormTarget>(typed.State.Session.Tabs[0].Form!.Target);
    }

    [Fact]
    public void GivenWritesRunningAtTheRoot_WhenPressingQ_ThenItAsksFirstAndEscOrQAnswerNoAndYQuits()
    {
        var running = WithRunningWrite(Tabbed());

        var asked = Press(running, "q");
        var viaEsc = Press(asked.State, "Esc");
        var viaQ = Press(asked.State, "q");
        var yes = Press(asked.State, "y");

        Assert.Empty(asked.Effects);
        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Contains("1 write is still running", asked.State.Confirm!.Message, StringComparison.Ordinal);
        Assert.Equal(InputMode.Normal, viaEsc.State.Mode);
        Assert.Null(viaEsc.State.Confirm);
        Assert.Empty(viaEsc.Effects);
        Assert.Equal(InputMode.Normal, viaQ.State.Mode);
        Assert.Null(viaQ.State.Confirm);
        Assert.Empty(viaQ.Effects);
        Assert.Single(yes.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenWritesRunningInsideAnIssue_WhenPressingQ_ThenTheIssueClosesWithoutAskingToQuit()
    {
        var detail = WithRunningWrite(Press(Tabbed(), "Enter").State);

        var popped = Press(detail, "q");

        Assert.Equal(InputMode.Normal, popped.State.Mode);
        Assert.Empty(popped.State.Session.Tabs[0].Detail);
        Assert.Empty(popped.Effects);
    }

    private static AppState WithRunningWrite(AppState state)
    {
        var running = new Operation(
            99,
            new AssignRequest("A-1", null),
            state.Session.Tabs[0].Id,
            OperationPhase.Sending,
            [],
            _at);

        return state with { Session = state.Session with { Operations = [running] } };
    }

    private AppState EditorWithPopup()
    {
        var tab = Tabbed().Session.Tabs[0];
        var editing = Tabbed() with
        {
            Mode = InputMode.Insert,
            Editor = new CommentEditor(tab.Id, "A-1", null, TextBuffer.Empty, string.Empty),
        };

        return Press(editing, "@", "o").State;
    }

    private AppState TransitionFields()
    {
        var done = new IssueStatus("3", "Done", "done");
        var resolution = new FieldMeta("resolution", "Resolution", true, "resolution", false, [new("10000", "Done")]);
        var transition = new Transition("41", "Done", done, true, true, [resolution]);
        var opened = Press(Tabbed(), "Space", "i", "s").State;
        var tab = opened.Session.Tabs[0];
        var pick = ((TransitionPickTarget)tab.Form!.Target).With([transition]);
        var loadedPicker = new FormMetadataLoaded(tab.Id, tab.Form.RequestId, pick.Fields(), pick, _at);
        var picker = _reducer.Reduce(opened, loadedPicker).State;
        var chosen = Press(picker, "Enter").State;
        var form = chosen.Session.Tabs[0].Form!;
        var loaded = new FormMetadataLoaded(tab.Id, form.RequestId, FormFieldMapper.FromTransition([resolution]));

        return _reducer.Reduce(chosen, loaded).State;
    }

    private AppState Build(string surface)
    {
        return surface switch
        {
            "issue" => Press(Tabbed(), "Enter").State,
            "zoom" => Press(Press(Tabbed(), "Enter").State, "Ctrl-j", "Space z").State,
            "visual" => Press(Tabbed(), "v").State,
            "rank-pickup" => WithTab(Tabbed(), tab => tab with { RankPickup = "A-1" }),
            "settings" => OpenSettings(),
            "settings-choice" => Press(SelectRow(OpenSettings(), "hints.statusBar"), "Enter").State,
            "profile-manager" => Palette(Tabbed(), "profile list"),
            "manager-from-settings" => Press(SelectProfilesRow(OpenSettings()), "Enter").State,
            "context-menu" => Press(Tabbed(), "Space a").State,
            "confirm" => _reducer.Reduce(Tabbed(), new ConfirmRequested(new ConfirmRequest("Do it?", ActionIds.Quit))).State,
            "form-navigate" => WithForm(form => form with { Navigable = true, Navigating = true }),
            "form-type-chooser" => WithForm(form => form with { TypeChoices = [new CreateIssueType("1", "Task", "", 0, false)] }),
            "form-discard-prompt" => DiscardAsked(),
            "messages" => StatusMessages.OpenHistory(Tabbed()),
            "messages-with-settings" => StatusMessages.OpenSettings(Tabbed()),
            "profile-form" => Press(Palette(Tabbed(), "profile add"), "w", "o", "r").State,
            "picker" => Tabbed() with
            {
                Mode = InputMode.Insert,
                Picker = new SourcePickerState("ab", 0, PickerScope.Boards, false, null),
            },
            "jump" => Tabbed() with { Mode = InputMode.Insert, Jump = new JumpState("A-", 0, null) },
            "flash" => Tabbed() with { Mode = InputMode.Insert, FlashJump = new FlashJumpState("a") },
            "palette" => Tabbed() with { Mode = InputMode.Command, PaletteText = "ab" },
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };
    }

    // Whether the surface is gone (or, for the nested ones, is one level less deep) after a closing key.
    private static bool Closed(string surface, AppState start, AppState after)
    {
        var tab = after.Session.Tabs[0];

        return surface switch
        {
            "issue" => tab.Detail.Count == 0,
            "zoom" => !PaneActions.IsZoomed(after) && tab.Detail.Count > 0,
            "visual" => tab.VisualAnchor is null,
            "rank-pickup" => tab.RankPickup is null,
            "settings" => after.SettingsScreen is null,
            "settings-choice" => after.SettingsScreen is { Choice: null },
            "profile-manager" => after.ProfileList is null && after.Mode == InputMode.Normal,
            "manager-from-settings" => after.ProfileList is null && after.SettingsScreen is not null,
            "context-menu" => after.ContextMenu is null && after.Mode == InputMode.Normal,
            "confirm" => after.Confirm is null && after.Mode == InputMode.Normal,
            "form-navigate" or "form-type-chooser" => tab.Form is null && after.Mode == InputMode.Normal,
            "form-discard-prompt" => tab.Form is not null && after.Confirm is null && start.Mode != after.Mode,
            "messages" or "messages-with-settings"
                => !after.MessagesOpen && !after.MessagesShowSettings && after.Mode == InputMode.Normal,
            _ => false,
        };
    }

    private static string TypedText(AppState state)
    {
        return state.Picker?.Text ?? state.Jump?.Text ?? state.FlashJump?.Text ?? state.PaletteText;
    }

    private AppState DiscardAsked()
    {
        var opened = WithForm(form => form);
        var tab = opened.Session.Tabs[0];

        return DiscardPrompts.Ask(opened, FormShell.DiscardRequest(tab.Form!, tab.Id));
    }

    private AppState WithForm(Func<FormState, FormState> shape)
    {
        var tabbed = Tabbed();
        var tab = tabbed.Session.Tabs[0];
        var opened = FormState.Opened(new AssignFormTarget("A-1", null), 1, null);
        var field = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, []);
        var form = shape(opened with { Fields = [field], IsLoading = false });

        return WithTab(tabbed, _ => tab with { Form = form }) with { Mode = InputMode.Insert };
    }

    private AppState OpenSettings()
    {
        return _reducer.Reduce(Tabbed(), new ActionRequested(ActionIds.SettingsOpen)).State;
    }

    private static AppState SelectRow(AppState state, string name)
    {
        var index = SettingsRows.Build(state).ToList().FindIndex(row => row.Setting?.Name == name);

        return state with { SettingsScreen = new SettingsScreenState(index) };
    }

    private static AppState SelectProfilesRow(AppState state)
    {
        var index = SettingsRows.Build(state).ToList().FindIndex(row => row.Kind == SettingsRowKind.Profiles);

        return state with { SettingsScreen = new SettingsScreenState(index) };
    }

    private static AppState WithTab(AppState state, Func<TabState, TabState> change)
    {
        var tab = change(state.Session.Tabs[0]);

        return state with { Session = state.Session with { Tabs = [tab] } };
    }

    private AppState Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter", _at)).State;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys.SelectMany(group => group.Split(' ')))
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key, _at));
        }

        return result;
    }

    private AppState Tabbed()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var issue = new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
        var page = new IssuePage([issue], null, null);

        var loaded = _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _at)).State;

        return WithTab(loaded, current => current with { Panes = new PaneState(_grid) });
    }
}
