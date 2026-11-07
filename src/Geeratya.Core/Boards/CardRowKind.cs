namespace Geeratya.Core.Boards;

/// <summary>What one row of a board card shows (<c>docs/internal/ux-screens.md</c>, "Board card").</summary>
public enum CardRowKind
{
    /// <summary>The top border, which carries the parent label of a card whose parent is elsewhere.</summary>
    Top,

    /// <summary>A line of the summary; <see cref="CardRow.Index"/> is the line.</summary>
    Summary,

    /// <summary>A blank row that separates the parts of a card.</summary>
    Blank,

    /// <summary>A row of label chips; <see cref="CardRow.Index"/> is the row.</summary>
    Labels,

    /// <summary>The key row: key, type word, counter, priority and assignee.</summary>
    Key,

    /// <summary>The bottom border of a card of its own.</summary>
    Bottom,

    /// <summary>The line under a member of a shared frame; under the last member it is the bottom of the frame.</summary>
    Divider,
}
