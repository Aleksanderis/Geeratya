using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;

namespace Geeratya.Core.Forms;

/// <summary>
/// The pane layout of the create form (<c>docs/internal/ux-screens.md</c>, "Create form"): which pane holds which field, which
/// optional fields are folded behind <c>Ctrl-f</c>, and where the navigate-state keys go. The title pane holds
/// Summary, the Description pane the description, the Details pane every other editable field (a sidebar from an
/// overlay width of 100 columns, else under the others), and a field the form cannot edit belongs to no pane (the
/// "Not editable here" pane lists it). The field order (Summary, Description, required fields with Parent first, the
/// optional ones) is the reading order that <c>Tab</c> follows. Pure.
/// </summary>
public static class CreatePanes
{
    /// <summary>
    /// How many Details fields show while the viewport is unknown (see <see cref="CreateDetailsFit"/>).
    /// </summary>
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
    /// Whether the field is folded away: a Details field past the ones that fit the pane
    /// (<see cref="CreateDetailsFit.Limit"/>), unless it is required, changed or has an error (those always show), while <see cref="FormState.ShowAllFields"/> is off.
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

        return DetailRank(form, index) >= Limit(form);
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
    /// The focus after a navigate-state motion, or null when <paramref name="action"/> is not one of them. Summary, the
    /// Description and every shown Details field are one list of fields in reading order (the order <c>Tab</c>
    /// follows), whatever the pane or the overlay width: <c>j</c>, <c>l</c>, <c>Ctrl-j</c> and <c>Ctrl-l</c> go to the
    /// next field, <c>k</c>, <c>h</c>, <c>Ctrl-k</c> and <c>Ctrl-h</c> to the previous one, without wrapping. A motion
    /// at either end keeps the focus.
    /// </summary>
    public static int? Move(FormState form, string? action)
    {
        return action switch
        {
            ActionIds.NavigateDown or ActionIds.NavigateRight or ActionIds.PaneFocusDown or ActionIds.PaneFocusRight
                => Along(form, 1),
            ActionIds.NavigateUp or ActionIds.NavigateLeft or ActionIds.PaneFocusUp or ActionIds.PaneFocusLeft
                => Along(form, -1),
            _ => null,
        };
    }

    private static int? Along(FormState form, int delta)
    {
        var shown = Enumerable.Range(0, form.Fields.Count)
            .Where(index => PaneOf(form.Fields[index]) is not null && !IsFolded(form, index))
            .ToList();
        var position = shown.IndexOf(form.Focus);
        var target = position + delta;

        return position < 0 || target < 0 || target >= shown.Count ? form.Focus : shown[target];
    }

    private static int DetailRank(FormState form, int index)
    {
        return Enumerable.Range(0, index).Count(earlier => PaneOf(form.Fields[earlier]) == PaneIds.Details);
    }

    // A field that folds when the user asks for fewer: an optional Details field past the first five.
    private static bool IsFoldable(FormState form, int index)
    {
        var field = form.Fields[index];

        return PaneOf(field) == PaneIds.Details && !field.Required && DetailRank(form, index) >= Limit(form);
    }

    private static int Limit(FormState form)
    {
        var count = form.Fields.Count(field => PaneOf(field) == PaneIds.Details);

        return CreateDetailsFit.Limit(form.DetailsRows, count);
    }

    internal static int LastShown(FormState form)
    {
        var shown = Shown(form, PaneIds.Details);

        return shown.Count > 0 ? shown[^1] : 0;
    }
}
