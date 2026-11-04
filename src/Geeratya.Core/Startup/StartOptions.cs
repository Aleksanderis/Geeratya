namespace Geeratya.Core.Startup;

/// <summary>The start choices of the command line: the profile to use for this run and the tab to open.</summary>
public sealed record StartOptions(string? Profile, StartTarget? Target);
