namespace Geeratya.Core.Keymap;

/// <summary>
/// One row of the which-key popup: a <paramref name="Key"/> and its <paramref name="Label"/>, or a blank separator
/// between clusters (both empty).
/// </summary>
public sealed record WhichKeyRow(string Key, string Label)
{
    public static WhichKeyRow Blank { get; } = new(string.Empty, string.Empty);

    public bool IsBlank => Key.Length == 0;
}
