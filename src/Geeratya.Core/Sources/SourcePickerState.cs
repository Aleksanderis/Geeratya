using Geeratya.Core.Sprints;
using Geeratya.Core.Text;

namespace Geeratya.Core.Sources;

/// <summary>The open source picker (Insert mode overlay). Rows are derived from the text and the loaded lists.</summary>
/// <param name="Text">The filter text typed so far.</param>
/// <param name="Selected">The highlighted row; clamped to the rows when read.</param>
/// <param name="Scope">Which rows are offered.</param>
/// <param name="AutoOpen">
/// A palette command waits for the lists: when they are loaded a unique name match opens its view at once and
/// anything else leaves the picker open with the text typed.
/// </param>
/// <param name="Notice">Why the picker is open instead of a view (no match, ambiguous name), or null.</param>
public sealed record SourcePickerState(string Text, int Selected, PickerScope Scope, bool AutoOpen, string? Notice)
{
    /// <summary>The board and sprints of the sprint switcher; null in every other scope.</summary>
    public SprintPickerData? Sprint { get; init; }

    /// <summary>The open tabs, one row each, taken when the list opened; null in every other scope.</summary>
    public IReadOnlyList<SourceChoice>? Tabs { get; init; }

    /// <summary>Units between the caret and the end of <see cref="Text"/> (<see cref="PromptText"/>); 0 is the end.</summary>
    public int Back { get; init; }
}
