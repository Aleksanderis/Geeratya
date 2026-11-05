namespace Geeratya.Platform.Clipboard;

/// <summary>A program that reads the text to copy from its standard input (Windows: from its arguments).</summary>
public sealed record ClipboardCommand(string Program, IReadOnlyList<string> Arguments);
