using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Text;

namespace Geeratya.Core.State;

/// <summary>
/// The profile part of the reducer: the <c>:profile</c> commands and the add-profile form. Pure; the host runs
/// the effects (secret store, profile file) and answers with <see cref="ProfilesChanged"/> or
/// <see cref="ProfileOperationFailed"/>. Switching the active profile replaces the connection: views and caches
/// are scoped to the active profile and are dropped with it.
/// </summary>
public static partial class ProfileReducer
{
    public const string RemoveAction = ActionIds.ProfileRemove;
    public const string SwitchAction = "profile.switch";

    private const string AddVerb = "add";
    private const string ListVerb = "list";
    private const string SwitchVerb = "switch";
    private const string RemoveVerb = "remove";
    private const string CheckVerb = "check";
    private const string EditVerb = "edit";
    private const string BusyMessage = "Still saving the previous profile change; try again in a moment.";
    private const string Usage = "Usage: profile [list] | add | edit [name] | switch <name> | remove <name> | check";

    private const string TokenMissing = "The API token cannot be empty.";

    private const string TokenForChangedSite =
        "Type the token to test a changed site or account: the stored one is sent only to the saved site.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Runs <c>profile ...</c>; <paramref name="arguments"/> is the text after the word profile.</summary>
    public static ReduceResult Palette(AppState state, string arguments)
    {
        var text = arguments.Trim();
        var space = text.IndexOf(' ');
        var verb = space < 0 ? text : text[..space];
        var name = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        if (state.ProfileBusy && verb is AddVerb or EditVerb or SwitchVerb or RemoveVerb)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (verb is SwitchVerb or RemoveVerb && name.Length == 0)
        {
            return Plain(Say(state, Usage));
        }

        return verb switch
        {
            AddVerb => Add(state),
            EditVerb => Edit(state, name.Length == 0 ? state.Profiles.ActiveName : name),
            "" or ListVerb => OpenManager(state),
            SwitchVerb => Switch(state, name),
            RemoveVerb => AskRemove(state, name),
            CheckVerb => CheckConnection(state, announce: true),
            _ => Plain(Say(state, Usage)),
        };
    }

    /// <summary>Opens the form for a new profile (from the palette, the list or the start screen).</summary>
    public static ReduceResult Add(AppState state)
    {
        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        return Plain(state with
        {
            Mode = InputMode.Insert,
            PendingKeys = string.Empty,
            Form = ProfileForm.Start,
            Message = null,
        });
    }

    /// <summary>Ctrl-e in the profile list: edits the highlighted profile.</summary>
    public static ReduceResult EditHighlighted(AppState state, int? row = null)
    {
        return Highlighted(state, row) is { } profile ? Edit(state, profile.Name) : Plain(state);
    }

    /// <summary>Opens the form of the named profile; the stored token is not read and stays hidden.</summary>
    public static ReduceResult Edit(AppState state, string? name)
    {
        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (state.Profiles.Profiles.Count == 0)
        {
            return Plain(Say(state, List(state.Profiles)));
        }

        if (state.Profiles.Find(name) is not { } profile)
        {
            return Plain(Say(state, $"No profile named {name}. {List(state.Profiles)}"));
        }

        var form = ProfileForm.For(profile);

        return Plain(state with { Mode = InputMode.Insert, PendingKeys = string.Empty, Form = form, Message = null });
    }

    /// <summary>Tab and Shift-Tab: moves the focus one field, wrapping around.</summary>
    public static AppState Focus(AppState state, int delta)
    {
        if (state.Form is not { } form)
        {
            return state;
        }

        var count = Enum.GetValues<ProfileFormField>().Length;
        var next = (ProfileFormField)(((int)form.Field + delta + count) % count);

        return state with { Form = Normalize(form) with { Field = next, Back = 0 } };
    }

    /// <summary>A click on a field: focuses it.</summary>
    public static AppState FocusField(AppState state, int index)
    {
        if (state.Form is not { } form || !Enum.IsDefined((ProfileFormField)index))
        {
            return state;
        }

        return state with { Form = Normalize(form) with { Field = (ProfileFormField)index, Back = 0 } };
    }

    /// <summary>
    /// Alt-t in the form: checks the typed site, account and token (or the stored one) without saving. The stored token
    /// is only sent to the saved site and account: a changed site or account needs a typed token.
    /// </summary>
    public static ReduceResult TestConnection(AppState state)
    {
        if (state.Form is not { } form || form.Testing)
        {
            return Plain(state);
        }

        var siteProblem = ValidateSite(form.SiteUrl);
        var emailProblem = ValidateEmail(form.Email.Trim());

        if (siteProblem is not null || emailProblem is not null)
        {
            var field = siteProblem is null ? ProfileFormField.Email : ProfileFormField.SiteUrl;

            return Plain(state with { Form = form with { Field = field, Back = 0, Error = siteProblem ?? emailProblem } });
        }

        var profile = new Profile(form.Name.Trim(), TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl, form.Email.Trim());
        var original = form.EditingName is null ? null : state.Profiles.Find(form.EditingName);
        var typed = form.Token.IsEmpty ? null : form.Token;
        var allowEnvironment = state.Profiles.Profiles.Count == 1;

        if (typed is null && original is null)
        {
            return Plain(state with { Form = form with { Field = ProfileFormField.Token, Back = 0, Error = TokenMissing } });
        }

        if (typed is null && original!.CredentialKey != profile.CredentialKey)
        {
            var asking = form with { Field = ProfileFormField.Token, Back = 0, Error = TokenForChangedSite };

            return Plain(state with { Form = asking });
        }

        var tested = form with { Testing = true, TestResult = null, TestFailed = false, Error = null };

        return new ReduceResult(
            state with { Form = tested },
            [new TestProfileEffect(profile, typed, original?.CredentialKey, allowEnvironment)]);
    }

    /// <summary>
    /// The answer of a connection test; dropped when the form is closed, was edited since (an edit ends the test) or
    /// now holds other values than the ones tested.
    /// </summary>
    public static ReduceResult Tested(AppState state, ProfileTested tested)
    {
        if (state.Form is not { Testing: true } form || !Matches(form, tested.Profile))
        {
            return Plain(state);
        }

        return Plain(state with
        {
            Form = form with { Testing = false, TestFailed = !tested.Succeeded, TestResult = tested.Message },
        });
    }

    /// <summary>Moves the highlight of the profile list one row (clamped).</summary>
    public static AppState MoveList(AppState state, int delta)
    {
        if (state.ProfileList is not { } list)
        {
            return state;
        }

        var last = Math.Max(0, state.Profiles.Profiles.Count - 1);

        var selected = Math.Clamp(ClampedRow(state, list) + delta, 0, last);

        return state with { ProfileList = new ProfileListState(selected) };
    }

    /// <summary>Enter in the profile list: switches to the highlighted profile (as the palette command does).</summary>
    public static ReduceResult SwitchHighlighted(AppState state, int? row = null)
    {
        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (Highlighted(state, row) is not { } profile)
        {
            return Plain(state);
        }

        // The active profile needs no switch: Enter on it leaves the manager.
        return state.Profiles.ActiveName == profile.Name ? Plain(Closed(state)) : Switch(Closed(state), profile.Name);
    }

    /// <summary>Ctrl-x in the profile list: asks to remove the highlighted profile (as the palette command does).</summary>
    public static ReduceResult RemoveHighlighted(AppState state, int? row = null)
    {
        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        return Highlighted(state, row) is { } profile ? AskRemove(state, profile.Name) : Plain(state);
    }

    /// <summary>Closes the profile list and leaves Insert mode.</summary>
    public static AppState Closed(AppState state)
    {
        return state with { Mode = InputMode.Normal, ProfileList = null, PendingKeys = string.Empty };
    }

    /// <summary>Typed or pasted text goes in at the caret of the focused field.</summary>
    public static AppState Type(AppState state, string text)
    {
        return text.Length == 0 ? state : Edit(state, prompt => prompt.Insert(text));
    }

    public static AppState Backspace(AppState state)
    {
        return Apply(state, PromptOperation.Backspace);
    }

    /// <summary>An editing key (caret moves, deletes) in the focused field.</summary>
    public static AppState Apply(AppState state, PromptOperation operation)
    {
        return Edit(state, prompt => prompt.Apply(operation));
    }

    /// <summary>Runs one edit on the focused field; a changed text re-validates, a caret move keeps the notes.</summary>
    public static AppState Edit(AppState state, Func<PromptText, PromptText> change)
    {
        if (state.Form is not { } form)
        {
            return state;
        }

        var (edited, changed) = EditField(form, change);

        return changed ? Typed(state, edited) : state with { Form = edited };
    }
}
