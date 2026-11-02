namespace Geeratya.Core.State;

/// <summary>
/// A pending confirmation: what will change, the action that runs on <c>y</c> and what it applies to. A write
/// carries the command to run on <c>y</c> in <paramref name="OnConfirm"/> (checked again when it runs, so a busy
/// issue is refused at that moment); the other confirms name an action id.
/// </summary>
public sealed record ConfirmRequest(
    string Message,
    string ActionOnConfirm,
    string? Target = null,
    Command? OnConfirm = null);
