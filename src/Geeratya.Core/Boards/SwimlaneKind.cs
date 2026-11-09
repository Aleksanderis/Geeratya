namespace Geeratya.Core.Boards;

/// <summary>How the columns layout groups the cards of a board into lanes (<c>view.swimlane</c>, #192).</summary>
public enum SwimlaneKind
{
    /// <summary>No lanes: every card of the board is shown.</summary>
    None,

    /// <summary>One lane per assignee, then Unassigned.</summary>
    Assignee,

    /// <summary>One lane per epic, then No epic.</summary>
    Epic,

    /// <summary>One lane per label (a card with several labels is in each), then No label (#892).</summary>
    Labels,
}
