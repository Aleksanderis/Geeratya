namespace Geeratya.Core.Text;

/// <summary>Picks the fill of a label chip: a stable colour per label text, from the theme's accent colours.</summary>
public static class LabelChips
{
    private static readonly TextRole[] _roles =
    [
        TextRole.LabelChip0,
        TextRole.LabelChip1,
        TextRole.LabelChip2,
        TextRole.LabelChip3,
        TextRole.LabelChip4,
    ];

    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>The chip role of a label. The same text always gives the same role (case-insensitive, like Jira's label match); the hash is fixed, never the per-process one of <see cref="string.GetHashCode()"/>.</summary>
    public static TextRole For(string label)
    {
        var hash = FnvOffset;

        foreach (var character in label)
        {
            hash = unchecked((hash ^ char.ToUpperInvariant(character)) * FnvPrime);
        }

        return _roles[hash % (uint)_roles.Length];
    }
}
