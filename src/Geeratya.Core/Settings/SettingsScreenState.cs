using Geeratya.Core.Lists;

namespace Geeratya.Core.Settings;

/// <summary>The open settings screen (<c>:settings</c>), a popup over the view of the active tab in Normal mode.</summary>
/// <param name="Pick">The highlighted row (<see cref="PickList"/>), an index into <see cref="SettingsRows.Build"/>; clamped when read.</param>
/// <param name="Choices">The highlighted word of the open choice list of the selected row, or null when no list is open.</param>
/// <param name="Preview">The theme currently drawn only as a preview (not saved), or null.</param>
public sealed record SettingsScreenState(PickList Pick, PickList? Choices = null, string? Preview = null)
{
    /// <summary>A screen with the highlight on row <paramref name="selected"/> and, when given, an open choice list.</summary>
    public SettingsScreenState(int selected, int? choice = null, string? preview = null)
        : this(new PickList(selected), choice is { } word ? new PickList(word) : null, preview)
    {
    }

    /// <summary>The highlighted row; clamped when read.</summary>
    public int Selected
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }

    /// <summary>The highlighted word of the open choice list, or null when no list is open.</summary>
    public int? Choice
    {
        get => Choices?.Selected;
        init => Choices = value is { } word ? new PickList(word) : null;
    }
}
