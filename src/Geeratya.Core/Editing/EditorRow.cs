namespace Geeratya.Core.Editing;

/// <summary>
/// One visual row of an edited text: the text between <paramref name="Start"/> and <paramref name="End"/> (exclusive,
/// without the newline that ends a logical line).
/// </summary>
public readonly record struct EditorRow(int Start, int End);
