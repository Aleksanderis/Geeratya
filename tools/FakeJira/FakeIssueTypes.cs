namespace Geeratya.FakeJira;

/// <summary>
/// The issue types the fake knows, by id, matching <c>metadata/createmeta-issuetypes-*.json</c>. The company-managed
/// project (GCM) and the team-managed one (GTM) use different ids for the same names; stories and bugs share the
/// fields of the recorded task.
/// </summary>
public static class FakeIssueTypes
{
    private const string Gcm = "GCM";
    private const string Gtm = "GTM";

    public static IReadOnlyList<FakeIssueType> All { get; } =
    [
        new("10000", "Epic", Gcm, 1, false, "metadata/createmeta-fields-gcm-epic.json"),
        new("10007", "Sub-task", Gcm, -1, true, "metadata/createmeta-fields-gcm-subtask.json"),
        new("10008", "Story", Gcm, 0, false, "metadata/createmeta-fields-gcm-task.json"),
        new("10009", "Bug", Gcm, 0, false, "metadata/createmeta-fields-gcm-task.json"),
        new("10010", "Task", Gcm, 0, false, "metadata/createmeta-fields-gcm-task.json"),
        new("10011", "Subtask", Gtm, -1, true, "metadata/createmeta-fields-gtm-subtask.json"),
        new("10012", "Epic", Gtm, 1, false, "metadata/createmeta-fields-gtm-epic.json"),
        new("10013", "Story", Gtm, 0, false, "metadata/createmeta-fields-gtm-task.json"),
        new("10014", "Bug", Gtm, 0, false, "metadata/createmeta-fields-gtm-task.json"),
        new("10015", "Task", Gtm, 0, false, "metadata/createmeta-fields-gtm-task.json"),
    ];

    /// <summary>The type of the project with this id, else (a create that names it) with this name; else null.</summary>
    public static FakeIssueType? Find(string? projectKey, string? id, string? name)
    {
        var ofProject = All.Where(type => type.ProjectKey == projectKey);

        if (id is not null)
        {
            return ofProject.FirstOrDefault(type => type.Id == id);
        }

        return name is null ? null : ofProject.FirstOrDefault(type => type.Name == name);
    }
}
