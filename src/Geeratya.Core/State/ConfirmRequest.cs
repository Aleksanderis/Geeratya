namespace Geeratya.Core.State;

/// <summary>
/// A pending confirmation: what will change, the action that runs on <c>y</c> and what it applies to. A write sets
/// <paramref name="OnConfirm"/> instead: the command the reducer runs on <c>y</c> (checked again at that moment).
/// </summary>
public sealed record ConfirmRequest(
    string Message,
    string ActionOnConfirm,
    string? Target = null,
    Command? OnConfirm = null);
