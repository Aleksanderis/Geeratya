namespace Geeratya.Core.State;

/// <summary>
/// The words every confirm and discard dialog shares (<c>docs/internal/ui-consistency.md</c>, decision 8): the one
/// sentence of an action that cannot be undone and the one question of a discard. Pure.
/// </summary>
public static class ConfirmWording
{
    /// <summary>The warning of a destructive dialog; the only place a dialog says anything about undo.</summary>
    public const string CannotUndo = "Jira cannot undo this.";

    /// <summary>The name of the <c>y</c> answer of a discard dialog.</summary>
    public const string DiscardLabel = "Discard";

    /// <summary>The discard question for <paramref name="subject"/> ("the new issue", "the changes to GCM-10").</summary>
    public static string Discard(string subject)
    {
        return $"Discard {subject}? Nothing has been saved.";
    }

    /// <summary>
    /// The warning line of <paramref name="request"/>: its own, else <see cref="CannotUndo"/>; empty when it is not
    /// destructive.
    /// </summary>
    public static string WarningOf(ConfirmRequest request)
    {
        if (!request.Destructive)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(request.Warning) ? CannotUndo : request.Warning;
    }
}
