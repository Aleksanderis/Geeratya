using Geeratya.Jira.Jql;

namespace Geeratya.Core.Views;

/// <summary>Which search a list source runs. Pure, so the loading code has one place that knows the sources.</summary>
public static class ViewQuery
{
    /// <summary>
    /// The JQL a list source loads: typed JQL as typed, a saved filter as <c>filter = &lt;id&gt;</c>. Null for the
    /// sources that load through the agile API (board, backlog; #78).
    /// </summary>
    public static string? JqlFor(ViewDefinition definition)
    {
        return definition.Source switch
        {
            ViewSourceKind.Jql => definition.SourceValue,
            ViewSourceKind.SavedFilter => JqlBuilder.Filter(definition.SourceValue),
            _ => null,
        };
    }
}
