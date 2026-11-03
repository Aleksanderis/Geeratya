using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The cards of the selected board column in display order, the index of the selected card and, per card, whether
/// it is shown under its parent (null when none is). <paramref name="Column"/> is the index of the selected column
/// among the <paramref name="ColumnCount"/> drawn ones.
/// </summary>
public sealed record ColumnSelection(
    IReadOnlyList<IssueSummary> Rows,
    int Index,
    IReadOnlyList<bool>? Nested,
    int Column = 0,
    int ColumnCount = 1);
