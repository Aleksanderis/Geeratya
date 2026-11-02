namespace Geeratya.Core.Jump;

/// <summary>The open jump prompt (Insert mode overlay). Rows are derived from the text and the session.</summary>
/// <param name="Text">The key (or part of one) typed so far.</param>
/// <param name="Selected">The highlighted row; clamped to the rows when read.</param>
/// <param name="Notice">Why Enter did not open anything (garbage, no project to complete a number), or null.</param>
public sealed record JumpState(string Text, int Selected, string? Notice);
