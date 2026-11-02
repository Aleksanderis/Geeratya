namespace Geeratya.Jira.Sprints.Models;

/// <summary>The lifecycle state of a sprint as Jira names it: <c>future</c>, <c>active</c>, <c>closed</c>.</summary>
public enum SprintState
{
    Unknown,
    Future,
    Active,
    Closed,
}
