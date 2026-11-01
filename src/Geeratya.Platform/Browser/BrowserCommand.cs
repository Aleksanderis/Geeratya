namespace Geeratya.Platform.Browser;

/// <summary>The program and argument list that opens a URL on one operating system.</summary>
public sealed record BrowserCommand(string Program, IReadOnlyList<string> Arguments);
