namespace Geeratya.Core.State;

/// <summary>
/// A pending confirmation: what will change, the action that runs on <c>y</c> and what it applies to. A write
/// carries the command to run on <c>y</c> in <paramref name="OnConfirm"/> (checked again when it runs, so a busy
/// issue is refused at that moment); the other confirms name an action id. <paramref name="Label"/> names the
/// <c>y</c> answer in the dialog ("Delete issue"); without one it reads "Confirm".
/// <paramref name="Destructive"/> marks an action that cannot be undone (every write to Jira, a removed profile): the
/// dialog frame and its warning line use the bad colour, and the dialog adds the one warning sentence itself
/// (<paramref name="Warning"/>, by default <see cref="ConfirmWording.CannotUndo"/>), so a message never carries its own
/// claim about undo; a question that loses nothing in Jira (discard, quit, switch profile) uses the warn colour
/// (<c>docs/internal/ui-consistency.md</c>, decision 8 as changed on 2026-11-18).
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
    string? Heading = null,
    string? Warning = null);
