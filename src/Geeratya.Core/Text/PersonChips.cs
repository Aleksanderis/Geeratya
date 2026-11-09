namespace Geeratya.Core.Text;

/// <summary>
/// Picks the fill of a person's initials chip (assignee, reporter, relation row, form person): a stable colour per
/// display name, from the same accent colours as <see cref="LabelChips"/>. The name is the key because every place that
/// shows a person has it, while only some have the account id; the same person is therefore the same colour everywhere.
/// </summary>
public static class PersonChips
{
    /// <summary>The chip role of <paramref name="name"/> (case-insensitive, fixed hash, see <see cref="LabelChips.For"/>).</summary>
    public static TextRole For(string name)
    {
        return LabelChips.For(name.Trim());
    }

    /// <summary>The padded chip text for <paramref name="initials"/>: a space each side, so <c>ChipCaps</c> can round it.</summary>
    public static string Text(string initials, int cells)
    {
        return $" {initials}{new string(' ', Math.Max(0, cells - DisplayWidth.Of(initials)))} ";
    }
}
