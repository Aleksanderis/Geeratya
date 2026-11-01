namespace Geeratya.Core.State;

/// <summary>A pending confirmation: what will change, the action that runs on <c>y</c> and what it applies to.</summary>
public sealed record ConfirmRequest(string Message, string ActionOnConfirm, string? Target = null);
