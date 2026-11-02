namespace Geeratya.Core.Sources;

/// <summary>Which rows the source picker offers. The palette commands open it narrowed to one kind.</summary>
public enum PickerScope
{
    /// <summary>Boards, backlogs, filters and the typed query (<c>&lt;space&gt;t</c>, <c>:tab new</c>).</summary>
    All,

    /// <summary>Boards only (<c>:board</c>).</summary>
    Boards,

    /// <summary>Backlogs only (<c>:backlog</c>).</summary>
    Backlogs,

    /// <summary>Saved filters only (<c>:filter</c>).</summary>
    Filters,

    /// <summary>Recent JQL strings and the typed query (<c>:jql</c> without a query).</summary>
    Jql,
}
