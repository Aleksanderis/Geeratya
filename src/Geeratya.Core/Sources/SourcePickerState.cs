using Geeratya.Core.Lists;
using Geeratya.Core.Sprints;
using Geeratya.Core.Text;

namespace Geeratya.Core.Sources;

/// <summary>The open source picker (Insert mode overlay). Rows are derived from the text and the loaded lists.</summary>
/// <param name="Text">The filter text typed so far.</param>
/// <param name="Pick">The highlighted row (<see cref="PickList"/>); clamped to the rows when read.</param>
/// <param name="Scope">Which rows are offered.</param>
/// <param name="AutoOpen">
/// A palette command waits for the lists: when they are loaded a unique name match opens its view at once and
/// anything else leaves the picker open with the text typed.
/// </param>
/// <param name="Notice">Why the picker is open instead of a view (no match, ambiguous name), or null.</param>
public sealed record SourcePickerState(string Text, PickList Pick, PickerScope Scope, bool AutoOpen, string? Notice)
{
    /// <summary>A picker with the highlight on row <paramref name="selected"/>.</summary>
    public SourcePickerState(string text, int selected, PickerScope scope, bool autoOpen, string? notice)
        : this(text, new PickList(selected), scope, autoOpen, notice)
    {
    }

    /// <summary>The highlighted row; clamped to the rows when read.</summary>
    public int Selected
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }

    /// <summary>The board and sprints of the sprint switcher; null in every other scope.</summary>
    public SprintPickerData? Sprint { get; init; }

    /// <summary>The open tabs, one row each, taken when the list opened; null in every other scope.</summary>
    public IReadOnlyList<SourceChoice>? Tabs { get; init; }

    /// <summary>Units between the caret and the end of <see cref="Text"/> (<see cref="PromptText"/>); 0 is the end.</summary>
    public int Back { get; init; }
}
