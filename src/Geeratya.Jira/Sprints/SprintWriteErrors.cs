using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Tells what a failed sprint write means. Jira answers 400 with plain English sentences (observed on the dev site,
/// "Scrum survey (#172)" in docs/jira-layer.md), so this looks for the stable parts of them; an unknown text is
/// <see cref="SprintWriteProblem.Other"/> and the caller shows <see cref="WriteError.Message"/>. There is no error for
/// "another sprint is active": Jira allows parallel active sprints on both project kinds.
/// </summary>
public static class SprintWriteErrors
{
    /// <summary>The message of the client's own end-before-start check.</summary>
    public const string EndBeforeStartMessage = "The end must be after the start.";

    private const string StartDateField = "startDate";
    private const string EndDateField = "endDate";
    private const string NameField = "name";
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

    private static SprintWriteProblem ClassifyField(WriteFieldError field) =>
        field.FieldId switch
        {
            StartDateField => SprintWriteProblem.StartDateMissing,
            EndDateField when field.Message == EndBeforeStartMessage => SprintWriteProblem.EndBeforeStart,
            EndDateField => SprintWriteProblem.EndDateMissing,
            NameField => SprintWriteProblem.NameMissing,
            _ => SprintWriteProblem.Other,
        };

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

        return message.Contains(ToClosedText, StringComparison.Ordinal)
            ? SprintWriteProblem.NotActive
            : SprintWriteProblem.Other;
    }
}
