using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Tells what a failed sprint write means. Jira answers 400 with plain English sentences (the start date and the
/// state transitions were recorded on the dev site, "Scrum survey (#172)" in docs/jira-layer.md), so this looks for
/// the stable parts of them; an unknown text is <see cref="SprintWriteProblem.Other"/> and the caller shows
/// <see cref="WriteError.Message"/>. The client's own checks use the two message constants below. There is no error
/// for "another sprint is active": Jira allows parallel active sprints on both project kinds.
/// </summary>
public static class SprintWriteErrors
{
    /// <summary>The message of the client's own end-before-start check.</summary>
    public const string EndBeforeStartMessage = "The end must be after the start.";

    /// <summary>The message of the client's own blank-name check.</summary>
    public const string NameMissingMessage = "A sprint needs a name.";

    private const string StartDateMissingText = "You must specify a start date";
    private const string TransitionText = "Transitioning from";
    private const string FromClosedText = "from CLOSED";
    private const string ToClosedText = "to CLOSED";

    public static SprintWriteProblem Classify(WriteError error)
    {
        if (error.Kind != WriteErrorKind.Validation)
        {
            return SprintWriteProblem.Other;
        }

        foreach (var field in error.FieldErrors)
        {
            var problem = ClassifyField(field);

            if (problem != SprintWriteProblem.Other)
            {
                return problem;
            }
        }

        return error.Messages.Select(ClassifyMessage).FirstOrDefault(problem => problem != SprintWriteProblem.Other);
    }

    private static SprintWriteProblem ClassifyField(WriteFieldError field)
    {
        if (field.FieldId == SprintWriteFields.StartDate
            && field.Message.Contains(StartDateMissingText, StringComparison.Ordinal))
        {
            return SprintWriteProblem.StartDateMissing;
        }

        return (field.FieldId, field.Message) switch
        {
            (SprintWriteFields.EndDate, EndBeforeStartMessage) => SprintWriteProblem.EndBeforeStart,
            (SprintWriteFields.Name, NameMissingMessage) => SprintWriteProblem.NameMissing,
            _ => SprintWriteProblem.Other,
        };
    }

    private static SprintWriteProblem ClassifyMessage(string message)
    {
        if (!message.Contains(TransitionText, StringComparison.Ordinal))
        {
            return SprintWriteProblem.Other;
        }

        if (message.Contains(FromClosedText, StringComparison.Ordinal))
        {
            return SprintWriteProblem.AlreadyClosed;
        }

        if (message.Contains(ToClosedText, StringComparison.Ordinal))
        {
            return SprintWriteProblem.NotActive;
        }

        return SprintWriteProblem.Other;
    }
}
