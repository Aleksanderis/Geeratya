using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>What the fake remembers about one issue: a created issue, or edits laid over a recorded one.</summary>
public sealed class StoredIssue
{
    public StoredIssue(string key, string id, bool isCreated)
    {
        Key = key;
        Id = id;
        IsCreated = isCreated;
    }

    public string Key { get; }

    public string Id { get; }

    /// <summary>True for an issue made by a create call; false for an edit layer over a recorded issue.</summary>
    public bool IsCreated { get; }

    public string IssueType { get; set; } = "Task";

    /// <summary>The new value of each edited field, by field id.</summary>
    public Dictionary<string, JsonNode?> Fields { get; } = [];

    /// <summary>The status object after a transition; null while the recorded or initial status stands.</summary>
    public JsonNode? Status { get; set; }

    public List<JsonObject> Comments { get; } = [];

    public HashSet<string> DeletedCommentIds { get; } = [];
}
