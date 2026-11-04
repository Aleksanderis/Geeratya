using Geeratya.Core.Text;

namespace Geeratya.Core.Flash;

/// <summary>The open flash jump prompt (Insert mode): the text typed so far; the labels are derived from it.</summary>
/// <param name="Text">The text to look for in the visible rows.</param>
public sealed record FlashJumpState(string Text)
{
    /// <summary>Units between the caret and the end of <see cref="Text"/> (<see cref="PromptText"/>); 0 is the end.</summary>
    public int Back { get; init; }
}
