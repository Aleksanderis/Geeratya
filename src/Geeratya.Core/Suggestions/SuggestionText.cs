using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>How one candidate reads in a list and in a field: an issue as <c>KEY  summary</c>, others by name.</summary>
public static class SuggestionText
{
    private const string Inactive = " (inactive)";
    private const string NewValue = " (new)";

    public static string Of(Suggestion suggestion)
    {
        var name = suggestion.Detail is { Length: > 0 } detail && detail != SuggestionRanker.NewValueDetail
            && detail != AssigneeQuickRows.MeDetail
            ? $"{detail}  {suggestion.Label}"
            : suggestion.Label;

        return suggestion.IsInactive ? $"{name}{Inactive}" : name;
    }

    /// <summary>The text of a list row: as <see cref="Of"/>, and a typed value marked as new.</summary>
    public static string Row(Suggestion suggestion)
    {
        return suggestion.Detail switch
        {
            SuggestionRanker.NewValueDetail => $"{suggestion.Label}{NewValue}",
            AssigneeQuickRows.MeDetail => suggestion.Label.Length == 0 ? "Me" : $"Me ({suggestion.Label})",
            _ => Of(suggestion),
        };
    }
}
