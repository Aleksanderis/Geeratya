using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Startup;

namespace Geeratya.Core.State;

public static partial class ProfileReducer
{
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
            return Plain(state with { Form = Normalize(form) with { Field = form.Field + 1, Back = 0, Error = null } });
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
            return Plain(state with { Form = form with { Field = problem.Field, Back = 0, Error = problem.Text } });
        }

        var profile = new Profile(form.Name.Trim(), TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl, form.Email.Trim());
        var original = form.EditingName is null ? null : state.Profiles.Find(form.EditingName);
        var busy = state with
        {
            Mode = state.ProfileList is null ? InputMode.Normal : InputMode.Insert,
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
        var effects = new List<Effect>();

        if (next.ProfileList is not null)
        {
            effects.AddRange(TokenSourceEffects(next));
        }

        if (previous == current && !changed.Reconnect)
        {
            return new ReduceResult(next, effects);
        }

        var replaced = next with { Session = SiteSession.Empty(current) };

        if (previous is not null)
        {
            effects.Add(new DropSiteCacheEffect(previous));
        }

        if (current is not null)
        {
            replaced = SetConnection(replaced, new Checking());
            effects.Add(new CheckConnectionEffect(current));
        }

        return StartupReducer.Begin(new ReduceResult(replaced, effects));
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

        return StartupReducer.Connected(Plain(checkedState));
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
}
