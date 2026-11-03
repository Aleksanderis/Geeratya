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
    private const string BusyMessage = "Still saving the previous profile change; try again in a moment.";
    private const string Usage = "Usage: profile add | list | switch <name> | remove <name> | check";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Runs <c>profile ...</c>; <paramref name="arguments"/> is the text after the word profile.</summary>
    public static ReduceResult Palette(AppState state, string arguments)
    {
        var text = arguments.Trim();
        var space = text.IndexOf(' ');
        var verb = space < 0 ? text : text[..space];
        var name = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        if (state.ProfileBusy && verb is AddVerb or SwitchVerb or RemoveVerb)
        {
            return Plain(Say(state, BusyMessage));
        }

        if (verb is SwitchVerb or RemoveVerb && name.Length == 0)
        {
            return Plain(Say(state, Usage));
        }

        return verb switch
        {
            AddVerb => Plain(state with { Mode = InputMode.Insert, Form = ProfileForm.Start }),
            "" or ListVerb => OpenList(state),
            SwitchVerb => Switch(state, name),
            RemoveVerb => AskRemove(state, name),
            CheckVerb => CheckConnection(state, announce: true),
            _ => Plain(Say(state, Usage)),
        };
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

        return state with { Form = Edit(form, current => $"{current}{text}", secret => secret.Append(text)) };
    }

    public static AppState Backspace(AppState state)
    {
        var form = state.Form;

        if (form is null)
        {
            return state;
        }

        return state with { Form = Edit(form, RemoveLastElement, secret => secret.RemoveLast()) };
    }

    /// <summary>Validates the current field; on the last one the profile is added through an effect.</summary>
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

        var profile = new Profile(form.Name.Trim(), form.SiteUrl, form.Email.Trim());
        var catalog = state.Profiles.With(profile);
        var normal = state with
        {
            Mode = InputMode.Normal,
            Form = null,
            Message = $"Saving profile {profile.Name}...",
            ProfileBusy = true,
        };

        return new ReduceResult(normal, [new AddProfileEffect(profile, form.Token, catalog)]);
    }

    public static ReduceResult Changed(AppState state, ProfilesChanged changed)
    {
        var next = state with { Profiles = changed.Catalog, Message = changed.Message, ProfileBusy = false };

        var previous = state.Profiles.Active;
        var current = changed.Catalog.Active;

        if (previous == current)
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

        return Plain(SetConnection(state, ConnectionText.Setup(failed.Message)) with { Message = failed.Message });
    }

    public static ReduceResult Failed(AppState state, ProfileOperationFailed failed)
    {
        return Plain(state with { Message = failed.Message, ProfileBusy = false });
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
            return Plain(Say(state, List(state.Profiles)));
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
        return form.Field switch
        {
            ProfileFormField.Name => ValidateName(form.Name.Trim(), catalog),
            ProfileFormField.SiteUrl => ValidateSite(form.SiteUrl),
            ProfileFormField.Email => ValidateEmail(form.Email.Trim()),
            _ => form.Token.IsEmpty ? "The API token cannot be empty." : null,
        };
    }

    private static string? ValidateName(string name, ProfileCatalog catalog)
    {
        if (name.Length == 0)
        {
            return "The name cannot be empty.";
        }

        return catalog.Find(name) is null ? null : $"A profile named {name} already exists.";
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

    private static ProfileForm Edit(ProfileForm form, Func<string, string> text, Func<SecretText, SecretText> secret)
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
}
