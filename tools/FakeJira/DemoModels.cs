using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>An invented person of the demo team.</summary>
public sealed record DemoPerson(string AccountId, string Name);

/// <summary>An invented comment: author index into <see cref="DemoDataset.People"/>, age, markup (see <see cref="DemoAdf"/>).</summary>
public sealed record DemoComment(int Author, double HoursAgo, string Text);

/// <summary>An invented link to another issue; the issue holding it is the outward side.</summary>
public sealed record DemoLink(string Type, string Outward, string Inward, int Other);

/// <summary>One issue of the demo dataset; edits and transitions change it in memory.</summary>
public sealed class DemoIssue
{
    public int Number { get; init; }

    public string Type { get; set; } = "Task";

    public string Summary { get; set; } = string.Empty;

    public string Status { get; set; } = "To Do";

    public string Priority { get; set; } = "Medium";

    /// <summary>Index into the people list; -1 for unassigned.</summary>
    public int Assignee { get; set; } = -1;

    public int Reporter { get; init; }

    public List<string> Labels { get; set; } = [];

    public int? Points { get; set; }

    /// <summary>Number of the parent epic; 0 for none.</summary>
    public int Epic { get; set; }

    /// <summary>Sprint number (14 closed, 15 active, 16 future); 0 for the backlog.</summary>
    public int Sprint { get; set; }

    public double CreatedDaysAgo { get; init; } = 20;

    public DateTimeOffset Updated { get; set; }

    public JsonNode Description { get; set; } = new JsonObject();

    public List<DemoComment> Comments { get; } = [];

    public List<DemoLink> Links { get; } = [];

    public string Key => $"{DemoDataset.ProjectKey}-{Number}";
}
