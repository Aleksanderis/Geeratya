using System.Runtime.CompilerServices;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The marked issue keys of a tab as a case-insensitive set. A tab's mark list is replaced, never edited, when marks
/// change, so the set is built once per list instance and reused by every frame until the list is replaced.
/// </summary>
public static class MarkSet
{
    private static readonly HashSet<string> _none = [];
    // A weak table, not a collection: it cannot take a collection expression.
#pragma warning disable IDE0028
    private static readonly ConditionalWeakTable<IReadOnlyList<string>, HashSet<string>> _sets = new();
#pragma warning restore IDE0028

    public static HashSet<string> Of(IReadOnlyList<string> marks)
    {
        return marks.Count == 0 ? _none : _sets.GetValue(marks, Build);
    }

    private static HashSet<string> Build(IReadOnlyList<string> marks)
    {
        return new HashSet<string>(marks, StringComparer.OrdinalIgnoreCase);
    }
}
