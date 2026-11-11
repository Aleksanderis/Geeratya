namespace Geeratya.Jira.Search;

/// <summary>
/// Property names of the small objects inside a search row, defined once for the hand-written readers
/// (<see cref="WireObjectReader"/>, <see cref="FieldsConverter"/>) and the mapper. The names of the row's own
/// fields are in <see cref="IssueFields"/>. The readers are authoritative for parsing: the wire records carry no
/// serializer attributes for these names.
/// </summary>
internal static class WireNames
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Key = "key";
    public const string State = "state";
    public const string StatusCategory = "statusCategory";
    public const string HierarchyLevel = "hierarchyLevel";
    public const string Subtask = "subtask";
    public const string AccountId = "accountId";
    public const string DisplayName = "displayName";
}
