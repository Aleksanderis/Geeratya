using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// What the sprint switcher (the source picker in <c>PickerScope.Sprints</c>) knows about its board: where it was
/// opened from, the sprints once loaded and how many closed ones are shown.
/// </summary>
/// <param name="BoardId">The Scrum board whose sprints are offered.</param>
/// <param name="Origin">The tab the picker was opened from; Enter switches it when it still is a Sprint view.</param>
/// <param name="Sprints">Every sprint of the board in Jira's order, null while loading or after a failure.</param>
/// <param name="Error">Why the load failed, or null.</param>
/// <param name="ClosedShown">How many closed sprints are listed (newest first); "more" adds a page.</param>
public sealed record SprintPickerData(
    long BoardId,
    TabId Origin,
    IReadOnlyList<Sprint>? Sprints,
    string? Error,
    int ClosedShown)
{
    /// <summary>
    /// The issues to move when the picker chooses the target of a sprint move (active and future sprints only);
    /// null for the switcher.
    /// </summary>
    public IReadOnlyList<string>? MoveKeys { get; init; }
}
