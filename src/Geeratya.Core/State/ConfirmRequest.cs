namespace Geeratya.Core.State;

/// <summary>
/// A pending confirmation: what will change, the action that runs on <c>y</c> and what it applies to. A write
/// carries the command to run on <c>y</c> in <paramref name="OnConfirm"/> (checked again when it runs, so a busy
/// issue is refused at that moment); the other confirms name an action id. <paramref name="Label"/> names the
/// <c>y</c> answer in the dialog ("Delete issue"); without one it reads "Confirm".
/// <paramref name="Destructive"/> marks an action Jira cannot undo or that removes data (delete, remove): the dialog
/// frame uses the bad colour, any other question the warn colour.
/// A <paramref name="Heading"/> names the action and its target in the title chip ("Move GCM-8"); without one the
/// chip reads "Confirm".
/// A <paramref name="Title"/> makes the request a notice: a read-only text under that title (not split into question
/// and warning, newlines kept) with a single close row; nothing runs on any answer.
/// </summary>
public sealed record ConfirmRequest(
    string Message,
    string ActionOnConfirm,
    string? Target = null,
    Command? OnConfirm = null,
    string? Label = null,
    string? Title = null,
    bool Destructive = false,
    string? Heading = null);
