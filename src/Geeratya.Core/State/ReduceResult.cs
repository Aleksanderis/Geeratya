namespace Geeratya.Core.State;

/// <summary>The new state plus the effects to run.</summary>
public sealed record ReduceResult(AppState State, IReadOnlyList<Effect> Effects);
