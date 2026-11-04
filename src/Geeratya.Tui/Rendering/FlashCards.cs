using Geeratya.Core.Flash;

namespace Geeratya.Tui.Rendering;

/// <summary>The flash jump labels of the cards in view in the columns layout, by issue key, and the typed text.</summary>
/// <param name="Labels">The label of each labelled card.</param>
/// <param name="Text">The text that was typed.</param>
public sealed record FlashCards(IReadOnlyDictionary<string, FlashLabel> Labels, string Text);
