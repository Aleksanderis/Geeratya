namespace Geeratya.Jira.Sprints.Models;

/// <summary>
/// What a failed sprint write means for the person, found from Jira's reply or the client's own check. Anything
/// else is <see cref="Other"/>: show the error message.
/// </summary>
public enum SprintWriteProblem
{
    Other,

    /// <summary>The sprint has no start date (starting or completing needs one).</summary>
    StartDateMissing,

    /// <summary>The end is not after the start.</summary>
    EndBeforeStart,

    /// <summary>Completing a sprint that is not active (a future sprint cannot be closed).</summary>
    NotActive,

    /// <summary>Starting or reopening a sprint that is already closed.</summary>
    AlreadyClosed,

    /// <summary>The sprint name is empty.</summary>
    NameMissing,
}
