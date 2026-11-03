using Geeratya.Core.Keymap;

namespace Geeratya.Tui;

/// <summary>Applies the user's <c>keymap.json</c> to the shared keymap table.</summary>
public static class UserKeymaps
{
    /// <summary>
    /// Replaces the bindings of <paramref name="keymap"/> with <paramref name="defaults"/> plus the user's file. Returns the load result
    /// (its problems and notice), or null when there is no store.
    /// </summary>
    public static KeymapLoadResult? Apply(KeymapTable keymap, KeymapTable defaults, UserKeymapStore? store)
    {
        if (store is null)
        {
            return null;
        }

        var result = store.Load(defaults.Bindings);

        keymap.Replace(result.Bindings, result.Custom);

        return result;
    }
}
