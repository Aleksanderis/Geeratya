using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;

namespace Geeratya.Core.Forms;

/// <summary>
/// The pane layout of the create form (<c>docs/ux-screens.md</c>, "Create form"): which pane holds which field, which
/// optional fields are folded behind <c>Ctrl-f</c>, and where the navigate-state keys go. The title pane holds
/// Summary, the Description pane the description, the Details pane every other editable field (a sidebar from an
/// overlay width of 100 columns, else under the others), and a field the form cannot edit belongs to no pane (the
/// "Not editable here" pane lists it). The field order (Summary, Description, required fields with Parent first, the
/// optional ones) is the reading order that <c>Tab</c> follows. Pure.
/// </summary>
public static class CreatePanes
{
    /// <summary>How many Details fields show before the rest is folded (required ones always show).</summary>
    public const int ShownDetails = 5;

    /// <summary>The pane of a field, or null for a field the form cannot edit.</summary>
    public static string? PaneOf(FormField field)
    {
        if (!field.IsEditable)
        {
            return null;
        }

        if (field.Id == FormFieldIds.Summary)
        {
            return PaneIds.Title;
        }

        return field is { Id: FormFieldIds.Description, Kind: FormFieldKind.Paragraph }
            ? PaneIds.Description
            : PaneIds.Details;
    }

    /// <summary>True for a create form that has chosen its type and has fields, which is drawn as panes.</summary>
    public static bool Applies(FormState form)
    {
        return form is { Target: CreateFormTarget { IssueType: not null }, IsLoading: false, IsChoosingType: false }
            && form.Fields.Count > 0;
    }

    /// <summary>
    /// Whether the field is folded away: a Details field past the first <see cref="ShownDetails"/>, unless it is
    /// required, changed or has an error (those always show), while <see cref="FormState.ShowAllFields"/> is off.
    /// </summary>
    public static bool IsFolded(FormState form, int index)
    {
        if (form.ShowAllFields || form.Target is not CreateFormTarget || index < 0 || index >= form.Fields.Count)
        {
            return false;
        }

        var field = form.Fields[index];

        if (PaneOf(field) != PaneIds.Details || field.Required || field.Error is not null || field.IsChanged)
        {
            return false;
        }

        return DetailRank(form, index) >= ShownDetails;
    }

    /// <summary>The indexes of the fields of <paramref name="pane"/> that show, in order.</summary>
    public static List<int> Shown(FormState form, string pane)
    {
        return [.. Enumerable.Range(0, form.Fields.Count)
            .Where(index => PaneOf(form.Fields[index]) == pane && !IsFolded(form, index))];
    }

    /// <summary>The indexes of the optional Details fields that are folded now.</summary>
    public static List<int> Folded(FormState form)
    {
        return [.. Enumerable.Range(0, form.Fields.Count).Where(index => IsFolded(form, index))];
    }

    /// <summary>True when folding hides at least one field (so <c>Ctrl-f</c> has something to do).</summary>
    public static bool CanFold(FormState form)
    {
        return form.Target is CreateFormTarget && (form.ShowAllFields
            ? Enumerable.Range(0, form.Fields.Count).Any(index => IsFoldable(form, index))
            : Folded(form).Count > 0);
    }

    /// <summary>The form with <c>Ctrl-f</c> applied: the folded fields shown, or shown again folded.</summary>
    public static FormState ToggleMore(FormState form)
    {
        if (!CanFold(form))
        {
            return form;
        }

        var toggled = form with { ShowAllFields = !form.ShowAllFields };

        return IsFolded(toggled, toggled.Focus) ? toggled with { Focus = LastShown(toggled), Suggest = null } : toggled;
    }

    /// <summary>The pane that holds the focused field, or null.</summary>
    public static string? FocusedPane(FormState form)
    {
        return form.Focus >= 0 && form.Focus < form.Fields.Count ? PaneOf(form.Fields[form.Focus]) : null;
    }

    /// <summary>
    /// The focus after a navigate-state motion, or null when <paramref name="action"/> is not one of them. <c>j</c> and
    /// <c>k</c> move inside the focused pane (not wrapping), <c>h</c>/<c>l</c> and the pane keys go to the nearest
    /// field of the pane in that direction (one column below 100 columns: <c>Ctrl-j</c>/<c>Ctrl-k</c> step through
    /// the panes, <c>h</c>/<c>l</c> do nothing). A motion that has nowhere to go keeps the focus.
    /// </summary>
    public static int? Move(FormState form, int overlayWidth, string? action)
    {
        return action switch
        {
            ActionIds.NavigateDown => Within(form, 1),
            ActionIds.NavigateUp => Within(form, -1),
            ActionIds.NavigateLeft or ActionIds.PaneFocusLeft => Across(form, overlayWidth, PaneDirection.Left),
            ActionIds.NavigateRight or ActionIds.PaneFocusRight => Across(form, overlayWidth, PaneDirection.Right),
            ActionIds.PaneFocusDown => Across(form, overlayWidth, PaneDirection.Down),
            ActionIds.PaneFocusUp => Across(form, overlayWidth, PaneDirection.Up),
            _ => null,
        };
    }

    /// <summary>The pane grid of the create form for an overlay <paramref name="overlayWidth"/> columns wide.</summary>
    public static PaneGrid Grid(FormState form, int overlayWidth)
    {
        var panes = new[] { PaneIds.Title, PaneIds.Description, PaneIds.Details }
            .Where(pane => Shown(form, pane).Count > 0)
            .ToList();
        var wide = DetailLayout.HasSidebar(overlayWidth);
        var slots = new List<PaneSlot>();
        var rows = panes.Count(pane => pane != PaneIds.Details);

        foreach (var pane in panes)
        {
            if (wide && pane == PaneIds.Details)
            {
                slots.Add(new PaneSlot(pane, 1, 0, 1, Math.Max(1, rows)));
            }
            else
            {
                slots.Add(new PaneSlot(pane, 0, slots.Count(slot => slot.Column == 0)));
            }
        }

        return new PaneGrid(slots);
    }

    private static int? Within(FormState form, int delta)
    {
        if (FocusedPane(form) is not { } pane)
        {
            return form.Focus;
        }

        var shown = Shown(form, pane);
        var position = shown.IndexOf(form.Focus);
        var target = position + delta;

        return position < 0 || target < 0 || target >= shown.Count ? form.Focus : shown[target];
    }

    private static int? Across(FormState form, int overlayWidth, PaneDirection direction)
    {
        if (FocusedPane(form) is not { } current)
        {
            return form.Focus;
        }

        var grid = Grid(form, overlayWidth);
        var target = PaneNavigator.Move(grid, current, direction);

        if (target == current)
        {
            return form.Focus;
        }

        var shown = Shown(form, target);

        // Leaving Details to the left lands on the title only from its first field, else on the Description below it.
        if (current == PaneIds.Details && direction == PaneDirection.Left && grid.Find(PaneIds.Description) is not null
            && Shown(form, PaneIds.Details).IndexOf(form.Focus) > 0)
        {
            shown = Shown(form, PaneIds.Description);
        }

        // Entering Details from above lands on its first field, from below on its last one.
        if (target == PaneIds.Details && direction == PaneDirection.Up)
        {
            return shown[^1];
        }

        return shown[0];
    }

    private static int DetailRank(FormState form, int index)
    {
        return Enumerable.Range(0, index).Count(earlier => PaneOf(form.Fields[earlier]) == PaneIds.Details);
    }

    // A field that folds when the user asks for fewer: an optional Details field past the first five.
    private static bool IsFoldable(FormState form, int index)
    {
        var field = form.Fields[index];

        return PaneOf(field) == PaneIds.Details && !field.Required && DetailRank(form, index) >= ShownDetails;
    }

    private static int LastShown(FormState form)
    {
        var shown = Shown(form, PaneIds.Details);

        return shown.Count > 0 ? shown[^1] : 0;
    }
}
