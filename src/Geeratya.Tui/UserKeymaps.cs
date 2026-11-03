using Geeratya.Core.Keymap;

namespace Geeratya.Tui;

/// <summary>Applies the user's <c>keymap.json</c> to the shared keymap table.</summary>
public static class UserKeymaps
{
    /// <summary>
    /// Replaces the bindings of <paramref name="keymap"/> with <paramref name="defaults"/> plus the user's file. Returns the status
    /// notice for the problems found, or null when there are none (or no store).
    /// </summary>
    public static string? Apply(KeymapTable keymap, KeymapTable defaults, UserKeymapStore? store)
    {
        if (store is null)
        {
            return null;
        }

        var result = store.Load(defaults.Bindings);

        keymap.Replace(result.Bindings, result.Custom);

        return result.Notice;
    }
}
