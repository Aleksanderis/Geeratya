namespace Geeratya.Core.Editing;

/// <summary>
/// One unit of an edited text: the text between <paramref name="Start"/> and <paramref name="End"/> (exclusive), shown
/// as <paramref name="Display"/> (the same text, except for a mention or issue link, which shows its short form).
/// </summary>
public readonly record struct EditorCell(int Start, int End, string Display);
