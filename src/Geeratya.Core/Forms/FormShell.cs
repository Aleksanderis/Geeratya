using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Forms;

/// <summary>
/// The one behaviour of every form, however it is drawn (box, create pane, Details dropdown, in-place edit;
/// <c>docs/internal/ui-consistency.md</c>, component 3): which forms have the navigate state, what Enter does on a
/// form of one field, the one discard prompt (a <see cref="ConfirmRequest"/>, the same call as the comment editor and
/// the profile form use through <see cref="DiscardPrompts"/>) and the one rule for a save with nothing changed. The
/// presentation decides only the layout. Pure.
/// </summary>
public static class FormShell
{
    /// <summary>The status line after a save with no changed field: the form closes and nothing is sent.</summary>
    public const string NothingChangedText = "Nothing changed.";

    /// <summary>The status line after the discard question was answered yes.</summary>
    public const string DiscardedText = "Discarded the changes";

    /// <summary>
    /// The state note of a navigable form that moves between fields, shown in the status line only (the mode badge
    /// says the rest); no field label or box title carries a state (owner decision 2026-11-18, #1034).
    /// </summary>
    public const string NavigateNote = "[navigate]";


    /// <summary>
    /// A form with more than one editable field has the navigate state besides editing (decision 11): <c>j</c>,
    /// <c>k</c> and Tab move between fields, Enter or <c>i</c> edits the focused one, <c>Esc</c> and <c>q</c> step back.
    /// A form of one field is always editing, there is nothing to move to.
    /// </summary>
    public static bool IsNavigable(IReadOnlyList<FormField> fields)
    {
        return fields.Count(static field => field.IsEditable) > 1;
    }

    /// <summary>
    /// The form has exactly one editable field: taking a row of a picked field with Enter saves it (a Details
    /// dropdown, the labels form, the assign form), while Tab only takes it.
    /// </summary>
    public static bool HasOneField(FormState form)
    {
        return form.Fields.Count(static field => field.IsEditable) == 1;
    }

    /// <summary>The discard question of a form with changes, naming what would be lost.</summary>
    public static ConfirmRequest DiscardRequest(FormState form, TabId tab)
    {
        return DiscardPrompts.Request(
            form.Target.DiscardSubject,
            DiscardPrompts.FormAction,
            new DiscardFormRequested(tab));
    }
}
