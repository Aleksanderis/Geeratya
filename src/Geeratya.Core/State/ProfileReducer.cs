using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

/// <summary>
/// The profile part of the reducer: the <c>:profile</c> commands and the add-profile form. Pure; the host runs
/// the effects (secret store, profile file) and answers with <see cref="ProfilesChanged"/> or
/// <see cref="ProfileOperationFailed"/>. Switching the active profile replaces the connection: views and caches
/// are scoped to the active profile and are dropped with it.
/// </summary>
public static class ProfileReducer
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
    private const string Usage = "Usage: profile add | edit [name] | list | switch <name> | remove <name> | check";

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
            "" or ListVerb => OpenList(state),
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

        return Plain(Closed(state) with { Mode = InputMode.Insert, Form = ProfileForm.Start, Message = null });
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

        return Plain(Closed(state) with { Mode = InputMode.Insert, Form = form, Message = null });
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

        return state with { Form = Normalize(form) with { Field = next } };
    }

    /// <summary>A click on a field: focuses it.</summary>
    public static AppState FocusField(AppState state, int index)
    {
        if (state.Form is not { } form || !Enum.IsDefined((ProfileFormField)index))
        {
            return state;
        }

        return state with { Form = Normalize(form) with { Field = (ProfileFormField)index } };
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

            return Plain(state with { Form = form with { Field = field, Error = siteProblem ?? emailProblem } });
        }

        var profile = new Profile(form.Name.Trim(), TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl, form.Email.Trim());
        var original = form.EditingName is null ? null : state.Profiles.Find(form.EditingName);
        var typed = form.Token.IsEmpty ? null : form.Token;
        var allowEnvironment = state.Profiles.Profiles.Count == 1;

        if (typed is null && original is null)
        {
            return Plain(state with { Form = form with { Field = ProfileFormField.Token, Error = TokenMissing } });
        }

        if (typed is null && original!.CredentialKey != profile.CredentialKey)
        {
            var asking = form with { Field = ProfileFormField.Token, Error = TokenForChangedSite };

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

        return Highlighted(state, row) is { } profile ? Switch(Closed(state), profile.Name) : Plain(state);
    }

    /// <summary>Ctrl-x in the profile list: asks to remove the highlighted profile (as the palette command does).</summary>
    public static ReduceResult RemoveHighlighted(AppState state, int? row = null)
    {
        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        return Highlighted(state, row) is { } profile ? AskRemove(Closed(state), profile.Name) : Plain(state);
    }

    /// <summary>Closes the profile list and leaves Insert mode.</summary>
    public static AppState Closed(AppState state)
    {
        return state with { Mode = InputMode.Normal, ProfileList = null, PendingKeys = string.Empty };
    }

    public static AppState Type(AppState state, string text)
    {
        var form = state.Form;

        if (form is null)
        {
            return state;
        }

        return Typed(state, EditField(form, current => $"{current}{text}", secret => secret.Append(text)));
    }

    public static AppState Backspace(AppState state)
    {
        var form = state.Form;

        if (form is null)
        {
            return state;
        }

        return Typed(state, EditField(form, RemoveLastElement, secret => secret.RemoveLast()));
    }

    /// <summary>Enter: validates the current field and moves on; on the last one the profile is saved.</summary>
    public static ReduceResult Submit(AppState state)
    {
        var form = state.Form;

        if (form is null)
        {
            return Plain(state);
        }

        var error = Validate(form, state.Profiles);

        if (error is not null)
        {
            return Plain(state with { Form = form with { Error = error } });
        }

        if (form.Field != ProfileFormField.Token)
        {
            return Plain(state with { Form = Normalize(form) with { Field = form.Field + 1, Error = null } });
        }

        return Save(state);
    }

    /// <summary>Ctrl-s: validates every field (focusing the first problem), then saves the profile by an effect.</summary>
    public static ReduceResult Save(AppState state)
    {
        if (state.Form is not { } form)
        {
            return Plain(state);
        }

        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (FirstProblem(form, state.Profiles) is { } problem)
        {
            return Plain(state with { Form = form with { Field = problem.Field, Error = problem.Text } });
        }

        var profile = new Profile(form.Name.Trim(), TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl, form.Email.Trim());
        var original = form.EditingName is null ? null : state.Profiles.Find(form.EditingName);
        var busy = state with
        {
            Mode = InputMode.Normal,
            Form = null,
            Message = $"Saving profile {profile.Name}...",
            ProfileBusy = true,
        };

        if (original is null)
        {
            return new ReduceResult(busy, [new AddProfileEffect(profile, form.Token, state.Profiles.With(profile))]);
        }

        var catalog = state.Profiles.Replace(original.Name, profile);
        var shared = catalog.Profiles.Any(other => other.CredentialKey == original.CredentialKey);
        var token = form.Token.IsEmpty ? null : form.Token;

        return new ReduceResult(busy, [new UpdateProfileEffect(original, profile, token, catalog, shared)]);
    }

    public static ReduceResult Changed(AppState state, ProfilesChanged changed)
    {
        var next = state with { Profiles = changed.Catalog, Message = changed.Message, ProfileBusy = false };

        var previous = state.Profiles.Active;
        var current = changed.Catalog.Active;

        if (previous == current && !changed.Reconnect)
        {
            return Plain(next);
        }

        var replaced = next with { Session = SiteSession.Empty(current) };
        var effects = new List<Effect>();

        if (previous is not null)
        {
            effects.Add(new DropSiteCacheEffect(previous));
        }

        if (current is not null)
        {
            replaced = SetConnection(replaced, new Checking());
            effects.Add(new CheckConnectionEffect(current));
        }

        return new ReduceResult(replaced, effects);
    }

    /// <summary>
    /// Starts the credential check of the active profile. <paramref name="announce"/> is true for the palette
    /// command, which says so when there is no profile; the start-up request stays silent.
    /// </summary>
    public static ReduceResult CheckConnection(AppState state, bool announce)
    {
        if (state.Profiles.Active is not { } profile)
        {
            return Plain(announce ? Say(state, "No profile yet: add one with :profile add.") : state);
        }

        return new ReduceResult(SetConnection(state, new Checking()), [new CheckConnectionEffect(profile)]);
    }

    /// <summary>A success for a profile that is no longer active is dropped.</summary>
    public static ReduceResult ConnectionChecked(AppState state, ConnectionChecked connected)
    {
        if (connected.Profile != state.Profiles.Active)
        {
            return Plain(state);
        }

        var name = ConnectionText.Sanitize(connected.User.DisplayName);
        var checkedState = SetConnection(state, new Connected(name, connected.User.AccountId));

        // Working credentials end the "rejected" stop of the automatic refresh.
        if (checkedState.Session.Link is Rejected)
        {
            checkedState = checkedState with { Session = checkedState.Session with { Link = new Online() } };
        }

        return Plain(checkedState);
    }

    /// <summary>A failure for a profile that is no longer active is dropped.</summary>
    public static ReduceResult ConnectionCheckFailed(AppState state, ConnectionCheckFailed failed)
    {
        if (failed.Profile != state.Profiles.Active)
        {
            return Plain(state);
        }

        return Plain(SetConnection(state, ConnectionText.Failure(failed.Error, failed.Profile.SiteUrl)));
    }

    private static AppState SetConnection(AppState state, ConnectionState connection)
    {
        return state with { Session = state.Session with { Connection = connection } };
    }

    /// <summary>A reply for a profile that is no longer active is dropped.</summary>
    public static ReduceResult ConnectionFailed(AppState state, ConnectionFailed failed)
    {
        if (failed.Profile != state.Profiles.Active)
        {
            return Plain(state);
        }

        var shown = SetConnection(state, ConnectionText.Setup(failed.Message));

        return Plain(StatusMessages.Fail(shown, failed.Message));
    }

    public static ReduceResult Failed(AppState state, ProfileOperationFailed failed)
    {
        return Plain(StatusMessages.Fail(state, failed.Message) with { ProfileBusy = false });
    }

    /// <summary>Runs the confirmed removal of the profile named in <paramref name="target"/>.</summary>
    public static ReduceResult Remove(AppState state, string? target)
    {
        var profile = state.Profiles.Find(target);

        if (state.ProfileBusy)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (profile is null)
        {
            return Plain(Say(state, $"No profile named {target}."));
        }

        var catalog = state.Profiles.Without(profile.Name);
        var shared = catalog.Profiles.Any(other => other.CredentialKey == profile.CredentialKey);

        return new ReduceResult(
            Say(state, $"Removing profile {profile.Name}...") with { ProfileBusy = true },
            [new RemoveProfileEffect(profile, !shared, catalog, state.Profiles.ActiveName == profile.Name)]);
    }

    /// <summary>Runs the switch the user confirmed while writes were running.</summary>
    public static ReduceResult ConfirmedSwitch(AppState state, string? target)
    {
        return Switch(state, target ?? string.Empty, confirmed: true);
    }

    private static ReduceResult Switch(AppState state, string name, bool confirmed = false)
    {
        var profile = state.Profiles.Find(name);

        if (profile is null)
        {
            return Plain(Say(state, $"No profile named {name}. {List(state.Profiles)}"));
        }

        if (state.Profiles.ActiveName == profile.Name)
        {
            return Plain(Say(state, $"{profile.Name} is already the active profile."));
        }

        if (!confirmed && WriteReducer.RunningText(state) is { } running)
        {
            var question = $"{running}. Switch to {profile.Name} anyway?";

            var request = new ConfirmRequest(question, SwitchAction, profile.Name, Label: "Switch profile");

            return Plain(state with { Mode = InputMode.Confirm, Confirm = request });
        }

        var message = $"Switched to {profile.Name} ({profile.SiteUrl}).";

        return new ReduceResult(
            Say(state, $"Switching to {profile.Name}...") with { ProfileBusy = true },
            [new SaveProfilesEffect(state.Profiles.Activate(profile.Name), message)]);
    }

    private static ReduceResult AskRemove(AppState state, string name)
    {
        var profile = state.Profiles.Find(name);

        if (profile is null)
        {
            return Plain(Say(state, $"No profile named {name}. {List(state.Profiles)}"));
        }

        var question = $"Remove profile {profile.Name} ({profile.Email} on {profile.SiteUrl}) and delete its stored "
            + $"token?{ActiveNotice(state.Profiles, profile)}{RunningNotice(state, profile)} This cannot be undone";
        var request = new ConfirmRequest(question, RemoveAction, profile.Name, Label: "Remove profile");

        return Plain(state with { Mode = InputMode.Confirm, Confirm = request });
    }

    /// <summary>Removing the active profile drops its session, so running writes lose their results too.</summary>
    private static string RunningNotice(AppState state, Profile profile)
    {
        if (state.Profiles.ActiveName != profile.Name || WriteReducer.RunningText(state) is not { } running)
        {
            return string.Empty;
        }

        return $" {running}.";
    }

    private static string ActiveNotice(ProfileCatalog catalog, Profile profile)
    {
        if (catalog.ActiveName != profile.Name)
        {
            return string.Empty;
        }

        var next = catalog.Without(profile.Name).ActiveName;

        return next is null ? " No profile will be active." : $" {next} becomes the active profile.";
    }

    private static ReduceResult OpenList(AppState state)
    {
        if (state.Profiles.Profiles.Count == 0)
        {
            return Add(state);
        }

        var active = state.Profiles.Profiles.ToList().FindIndex(profile => profile.Name == state.Profiles.ActiveName);

        return Plain(state with
        {
            Mode = InputMode.Insert,
            ProfileList = new ProfileListState(Math.Max(0, active)),
            Message = null,
        });
    }

    private static Profile? Highlighted(AppState state, int? row)
    {
        if (state.ProfileList is not { } list || state.Profiles.Profiles.Count == 0)
        {
            return null;
        }

        return state.Profiles.Profiles[Math.Clamp(row ?? list.Selected, 0, state.Profiles.Profiles.Count - 1)];
    }

    private static int ClampedRow(AppState state, ProfileListState list)
    {
        return Math.Clamp(list.Selected, 0, Math.Max(0, state.Profiles.Profiles.Count - 1));
    }

    private static string List(ProfileCatalog catalog)
    {
        if (catalog.Profiles.Count == 0)
        {
            return "No profiles yet. Add one with :profile add";
        }

        var names = catalog.Profiles.Select(profile => profile.Name == catalog.ActiveName ? $"{profile.Name} *" : profile.Name);

        return $"Profiles: {string.Join(", ", names)} (* active)";
    }

    private static string? Validate(ProfileForm form, ProfileCatalog catalog)
    {
        return Validate(form, form.Field, catalog);
    }

    private static string? Validate(ProfileForm form, ProfileFormField field, ProfileCatalog catalog)
    {
        return field switch
        {
            ProfileFormField.Name => ValidateName(form.Name.Trim(), form.EditingName, catalog),
            ProfileFormField.SiteUrl => ValidateSite(form.SiteUrl),
            ProfileFormField.Email => ValidateEmail(form.Email.Trim()),
            _ => form.Token.IsEmpty && !form.IsEdit ? TokenMissing : null,
        };
    }

    private static FieldProblem? FirstProblem(ProfileForm form, ProfileCatalog catalog)
    {
        foreach (var field in Enum.GetValues<ProfileFormField>())
        {
            if (Validate(form, field, catalog) is { } text)
            {
                return new FieldProblem(field, text);
            }
        }

        return null;
    }

    /// <summary>Validates as the user types: a field that is still empty shows nothing yet.</summary>
    private static AppState Typed(AppState state, ProfileForm form)
    {
        var empty = form.Field switch
        {
            ProfileFormField.Name => form.Name.Length == 0,
            ProfileFormField.SiteUrl => form.SiteUrl.Length == 0,
            ProfileFormField.Email => form.Email.Length == 0,
            _ => form.Token.IsEmpty,
        };

        var error = empty ? null : Validate(form, state.Profiles);

        var edited = form with { Error = error, Testing = false, TestResult = null, TestFailed = false };

        return state with { Form = edited };
    }

    private static string? ValidateName(string name, string? editing, ProfileCatalog catalog)
    {
        if (name.Length == 0)
        {
            return "The name cannot be empty.";
        }

        return catalog.Find(name) is null || name == editing ? null : $"A profile named {name} already exists.";
    }

    private static string? ValidateSite(string text)
    {
        return TryNormalizeSite(text) is null ? "Enter an https site address such as https://example.atlassian.net." : null;
    }

    private static string? ValidateEmail(string email)
    {
        var at = email.IndexOf('@');

        return at > 0 && at < email.Length - 1 && !email.Contains(' ') ? null : "Enter a valid email address.";
    }

    private static string? TryNormalizeSite(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        var withScheme = trimmed.Contains("://") ? trimmed : $"https://{trimmed}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.UserInfo.Length == 0 ? uri.GetLeftPart(UriPartial.Authority) : null;
    }

    private static ProfileForm Normalize(ProfileForm form)
    {
        return form.Field switch
        {
            ProfileFormField.Name => form with { Name = form.Name.Trim() },
            ProfileFormField.SiteUrl => form with { SiteUrl = TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl },
            ProfileFormField.Email => form with { Email = form.Email.Trim() },
            _ => form,
        };
    }

    private static ProfileForm EditField(
        ProfileForm form,
        Func<string, string> text,
        Func<SecretText, SecretText> secret)
    {
        return form.Field switch
        {
            ProfileFormField.Name => form with { Name = text(form.Name), Error = null },
            ProfileFormField.SiteUrl => form with { SiteUrl = text(form.SiteUrl), Error = null },
            ProfileFormField.Email => form with { Email = text(form.Email), Error = null },
            _ => form with { Token = secret(form.Token), Error = null },
        };
    }

    private static string RemoveLastElement(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var starts = StringInfo.ParseCombiningCharacters(text);

        return text[..starts[^1]];
    }

    private static AppState Say(AppState state, string message)
    {
        return state with { Message = message };
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }

    private static bool Matches(ProfileForm form, Profile tested)
    {
        var site = TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl;

        return site == tested.SiteUrl && form.Email.Trim() == tested.Email;
    }

    private sealed record FieldProblem(ProfileFormField Field, string Text);
}
