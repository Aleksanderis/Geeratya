namespace Geeratya.Core.Keymap;

/// <summary>
/// One row of the which-key popup: a <paramref name="Key"/> and its <paramref name="Label"/>, a group header
/// (<paramref name="IsHeader"/>, the name in <paramref name="Label"/>) or a blank separator between groups (both
/// empty).
/// </summary>
public sealed record WhichKeyRow(string Key, string Label, bool IsHeader = false)
{
    public static WhichKeyRow Blank { get; } = new(string.Empty, string.Empty);

    public bool IsBlank => !IsHeader && Key.Length == 0;

    public static WhichKeyRow Header(string name)
    {
        return new WhichKeyRow(string.Empty, name, true);
    }
}
