using Geeratya.Core.Lists;
using Geeratya.Core.Text;

namespace Geeratya.Core.Jump;

/// <summary>The open jump prompt (Insert mode overlay). Rows are derived from the text and the session.</summary>
/// <param name="Text">The key (or part of one) typed so far.</param>
/// <param name="Pick">The highlight of the completions (<see cref="PickList"/>); clamped to the rows when read.</param>
/// <param name="Notice">Why Enter did not open anything (garbage, no project to complete a number), or null.</param>
public sealed record JumpState(string Text, PickList Pick, string? Notice)
{
    /// <summary>A prompt with the highlight on row <paramref name="selected"/>.</summary>
    public JumpState(string text, int selected, string? notice)
        : this(text, new PickList(selected), notice)
    {
    }

    /// <summary>The highlighted row; clamped to the rows when read.</summary>
    public int Selected
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }

    /// <summary>Units between the caret and the end of <see cref="Text"/> (<see cref="PromptText"/>); 0 is the end.</summary>
    public int Back { get; init; }
}
