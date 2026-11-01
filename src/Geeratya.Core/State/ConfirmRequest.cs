namespace Geeratya.Core.State;

/// <summary>A pending confirmation: what will change, and the action that runs on <c>y</c>.</summary>
public sealed record ConfirmRequest(string Message, string ActionOnConfirm);
