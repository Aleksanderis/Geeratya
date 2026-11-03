using System.Globalization;
using Geeratya.Core.Views;

namespace Geeratya.Core.Sprints;

/// <summary>Which board a view belongs to, for the sprint switcher.</summary>
public static class SprintBoards
{
    /// <summary>The board of a board, backlog, Scrum backlog or Sprint view; null for filters and JQL.</summary>
    public static long? BoardOf(ViewDefinition definition)
    {
        if (definition.Source == ViewSourceKind.Sprint)
        {
            return SprintSource.TryParse(definition.SourceValue, out var sprint) ? sprint.BoardId : null;
        }

        if (!definition.Source.IsBoardBased())
        {
            return null;
        }

        var parsed = long.TryParse(definition.SourceValue, NumberStyles.None, CultureInfo.InvariantCulture, out var id);

        return parsed ? id : null;
    }
}
