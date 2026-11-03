namespace Geeratya.Core.Keymap;

/// <summary>The keymap to use and what was wrong with the user's file (empty when nothing).</summary>
public sealed record KeymapLoadResult(IReadOnlyList<KeyBinding> Bindings, IReadOnlyList<string> Problems)
{
    /// <summary>The rows of the file that were applied, in file order (unbinds first); skipped rows are not here.</summary>
    public IReadOnlyList<CustomKeymapRow> Custom { get; init; } = [];

    /// <summary>A one-line status notice for the problems, or null when there are none.</summary>
    public string? Notice
    {
        get
        {
            if (Problems.Count == 0)
            {
                return null;
            }

            var more = Problems.Count > 1 ? $" (and {Problems.Count - 1} more)" : string.Empty;

            return $"keymap.json: {Problems[0]}{more}";
        }
    }
}
