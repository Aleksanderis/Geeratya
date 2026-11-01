namespace Geeratya.Core.Views;

/// <summary>
/// An opaque tab id drawn from <c>AppState.Sequence</c>; never reused, also not across a profile switch.
/// </summary>
public readonly record struct TabId(long Value);
