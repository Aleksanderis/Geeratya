namespace Geeratya.Core.State;

/// <summary>Work the reducer asks the host to do; the reducer itself does none.</summary>
public abstract record Effect;

/// <summary>Leave the application.</summary>
public sealed record QuitEffect : Effect;
