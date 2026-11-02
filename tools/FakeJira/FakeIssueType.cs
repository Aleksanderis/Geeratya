namespace Geeratya.FakeJira;

/// <summary>
/// An issue type of the fake's two projects as the recorded create metadata lists it, with the create fixture that
/// holds its fields. <see cref="TakesParent"/> follows the recorded fields: epics have no parent field.
/// </summary>
public sealed record FakeIssueType(
    string Id,
    string Name,
    string ProjectKey,
    int HierarchyLevel,
    bool IsSubtask,
    string FieldsFixture)
{
    public bool TakesParent => HierarchyLevel <= 0;
}
