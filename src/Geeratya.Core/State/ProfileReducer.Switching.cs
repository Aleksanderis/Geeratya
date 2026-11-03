using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Startup;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

public static partial class ProfileReducer
{
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
        var request = new ConfirmRequest(
            question,
            RemoveAction,
            profile.Name,
            Label: "Remove profile",
            Destructive: true);

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

    /// <summary>
    /// Opens the profile manager screen (also with no profile: the first-run screen) and asks the host where each
    /// token comes from.
    /// </summary>
    public static ReduceResult OpenManager(AppState state)
    {
        var active = state.Profiles.Profiles.ToList().FindIndex(profile => profile.Name == state.Profiles.ActiveName);
        var opened = state with
        {
            Mode = InputMode.Insert,
            PendingKeys = string.Empty,
            ProfileList = new ProfileListState(Math.Max(0, active)),
            Message = null,
        };

        return new ReduceResult(opened, TokenSourceEffects(opened));
    }

    /// <summary>The state at start when there is no profile: the manager is the first screen. Keeps any notice.</summary>
    public static AppState FirstRun(AppState state)
    {
        if (state.Profiles.Profiles.Count > 0)
        {
            return state;
        }

        return state with
        {
            Mode = InputMode.Insert,
            ProfileList = new ProfileListState(0),
        };
    }

    /// <summary>
    /// The start does not open the board picker while the profile manager is open (it would sit on top of it). When the
    /// manager closes after the start is over, with nothing open, a connected profile and no tab, the picker opens now.
    /// </summary>
    public static ReduceResult ManagerClosed(AppState before, ReduceResult result)
    {
        var after = result.State;

        if (before.ProfileList is null || after.ProfileList is not null || after.Mode != InputMode.Normal
            || after.Session.Startup != StartupPhase.Done || after.Session.Tabs.Count > 0
            || after.ProfileBusy || after.Profiles.Active is null || after.Session.Connection is not Connected)
        {
            return result;
        }

        var opened = SourceReducer.OpenFirstBoard(after);

        return new ReduceResult(opened.State, [.. result.Effects, .. opened.Effects]);
    }

    private static IReadOnlyList<Effect> TokenSourceEffects(AppState state)
    {
        return state.Profiles.Profiles.Count == 0 ? _noEffects : [new LoadTokenSourcesEffect(state.Profiles.Profiles)];
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
}
