namespace Geeratya.Core.State;

/// <summary>The host tried to open a link: <paramref name="Error"/> is null on success, else the reason.</summary>
public sealed record UrlOpenFinished(string? Error, string? Label = null) : Command;
