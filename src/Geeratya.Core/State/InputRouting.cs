using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>Who takes a key, in the order <see cref="InputRouting"/> asks.</summary>
public enum KeyRouteKind
{
    /// <summary>The comment editor's suggestion popup takes the key (move, accept, close).</summary>
    SuggestionPopup,

    /// <summary>An open field form (create, edit, link, profile) takes every key.</summary>
    Form,

    /// <summary>A digit that starts or extends a count in Normal mode.</summary>
    Count,

    /// <summary>The help overlay's filter line is being typed and takes the printable keys.</summary>
    HelpFilter,

    /// <summary>The key starts or continues a longer binding; nothing runs yet.</summary>
    ChordPrefix,

    /// <summary>Esc in Normal mode with nothing to clear: pop the issue stack.</summary>
    BackOut,

    /// <summary>Esc in Normal mode with a notice showing: end the notice.</summary>
    Dismiss,

    /// <summary>The key completes a binding of the current mode; its action runs.</summary>
    Action,

    /// <summary>No binding: the key is text in Insert and Command mode and ignored elsewhere.</summary>
    Text,
}

/// <summary>Where a key goes and, when it resolved against the keymap, how.</summary>
/// <param name="Kind">The first layer that takes the key.</param>
/// <param name="Resolution">The keymap outcome; null for the layers that never consult the keymap.</param>
/// <param name="Popup">The popup's result when <see cref="Kind"/> is <see cref="KeyRouteKind.SuggestionPopup"/>.</param>
public sealed record KeyRoute(KeyRouteKind Kind, KeyResolution? Resolution = null, ReduceResult? Popup = null);

/// <summary>
/// The one ordered list of who is asked first when a key arrives (<c>docs/architecture.md</c>, "Input routing").
/// Overlays and editors come before global actions and the body, so an overlay swallows every key. Mouse gestures
/// and palette entries do not choose a layer: they end in an action request that runs the same handler a key
/// binding does (<c>Cancel</c> is the one that goes back through the Esc key path).
/// </summary>
public static class InputRouting
{
    /// <summary>The first layer that takes <paramref name="key"/> in <paramref name="state"/>.</summary>
    public static KeyRoute Route(AppState state, string key, KeymapTable keymap)
    {
        if (state.Editor?.Suggest is not null && state.Mode == InputMode.Insert
            && EditorSuggestions.Key(state, keymap.Resolve(state.Mode, state.PendingKeys, key).Action) is { } popup)
        {
            return new KeyRoute(KeyRouteKind.SuggestionPopup, Popup: popup);
        }

        if (FormReducer.IsOpen(state))
        {
            return new KeyRoute(KeyRouteKind.Form);
        }

        if (ReservedKeys.IsReserved(state.Mode, key))
        {
            return new KeyRoute(KeyRouteKind.Action, new KeyResolution(KeyResolutionKind.Action, ActionIds.Quit));
        }

        if (HelpReducer.TakesKeys(state) && keymap.Resolve(state.Mode, string.Empty, key).Action != ActionIds.Quit)
        {
            return new KeyRoute(KeyRouteKind.HelpFilter);
        }

        if (Counts.IsDigit(state, key))
        {
            return new KeyRoute(KeyRouteKind.Count);
        }

        var resolution = keymap.Resolve(state.Mode, state.PendingKeys, key);

        if (resolution.Kind == KeyResolutionKind.Prefix)
        {
            return new KeyRoute(KeyRouteKind.ChordPrefix, resolution);
        }

        if (IsBackOut(state, resolution))
        {
            return new KeyRoute(KeyRouteKind.BackOut, resolution);
        }

        if (IsDismiss(state, resolution))
        {
            return new KeyRoute(KeyRouteKind.Dismiss, resolution);
        }

        return new KeyRoute(resolution.Kind == KeyResolutionKind.None ? KeyRouteKind.Text : KeyRouteKind.Action, resolution);
    }

    /// <summary>Esc in Normal mode with nothing typed, no rank pickup and a notice showing ends the notice.</summary>
    private static bool IsDismiss(AppState state, KeyResolution resolution)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return resolution.Kind == KeyResolutionKind.Action
            && resolution.Action == ActionIds.Cancel
            && state.Mode == InputMode.Normal
            && state.PendingKeys.Length == 0
            && state.Count == 0
            && state.Editor is null
            && (state.Status is not null || state.Message is not null)
            && tab?.RankPickup is null;
    }

    /// <summary>Esc in Normal mode with nothing typed and no notice to clear pops the issue stack of the active tab.</summary>
    private static bool IsBackOut(AppState state, KeyResolution resolution)
    {
        return resolution.Kind == KeyResolutionKind.Action
            && resolution.Action == ActionIds.Cancel
            && state.Mode == InputMode.Normal
            && state.PendingKeys.Length == 0
            && state.Count == 0
            && state.Status is null
            && state.Message is null
            && ViewReducer.CanGoBack(state);
    }
}
