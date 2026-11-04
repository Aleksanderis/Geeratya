namespace Geeratya.Core.Startup;

/// <summary>One tab to open once the start of the session is over (<c>geeratya GEN-1</c>, <c>--board GCM</c>).</summary>
public sealed record StartTarget(StartTargetKind Kind, string Value);
