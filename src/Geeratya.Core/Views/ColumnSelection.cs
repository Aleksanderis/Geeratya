using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The cards of the selected board column in display order, the index of the selected card and, per card, whether
/// it is shown under its parent (null when none is).
/// </summary>
public sealed record ColumnSelection(
    IReadOnlyList<IssueSummary> Rows,
    int Index,
    IReadOnlyList<bool>? Nested);
