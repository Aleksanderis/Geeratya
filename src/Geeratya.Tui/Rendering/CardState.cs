namespace Geeratya.Tui.Rendering;

/// <summary>How a card is shown: under the cursor, followed by the selected card in its frame, marked, expanded.</summary>
public readonly record struct CardState(bool Selected, bool NextSelected, bool Marked, bool Open);
