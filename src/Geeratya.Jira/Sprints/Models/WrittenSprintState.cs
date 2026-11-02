namespace Geeratya.Jira.Sprints.Models;

/// <summary>The lifecycle state of a sprint as Jira reports it after a write.</summary>
public enum WrittenSprintState
{
    /// <summary>A state this app does not know; shown by name where needed, never an error.</summary>
    Unknown,
    Future,
    Active,
    Closed,
}
