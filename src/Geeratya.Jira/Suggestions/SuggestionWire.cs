namespace Geeratya.Jira.Suggestions;

/// <summary>The JSON shapes of the suggestion endpoints. Every member is optional.</summary>
internal static class SuggestionWire
{
    internal sealed record User(string? AccountId, string? DisplayName, bool? Active);

    internal sealed record LabelPage(List<string?>? Values, bool? IsLast);

    internal sealed record Picker(List<PickerSection?>? Sections);

    internal sealed record PickerSection(List<PickerIssue?>? Issues);

    internal sealed record PickerIssue(string? Key, string? Summary, string? SummaryText);

    internal sealed record TypeSearch(List<TypeIssue?>? Issues);

    internal sealed record TypeIssue(string? Key, TypeFields? Fields);

    internal sealed record TypeFields(TypeName? Issuetype);

    internal sealed record TypeName(string? Name);

    internal sealed record Named(string? Id, string? Name, bool? Archived);
}
