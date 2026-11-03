using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The fields of the sprint form. The ids are the member names of Jira's sprint body, so a field error of Jira
/// (<c>endDate</c>, <c>name</c>) lands on the field of the same id. Pure.
/// </summary>
public static class SprintFormFields
{
    public const string NameId = "name";
    public const string GoalId = "goal";
    public const string StartId = "startDate";
    public const string EndId = "endDate";

    /// <summary>The four fields, filled from <paramref name="sprint"/> when editing and empty when creating.</summary>
    public static IReadOnlyList<FormField> From(Sprint? sprint)
    {
        return
        [
            Field(NameId, "Name", FormFieldKind.Text, true, sprint?.Name),
            Field(GoalId, "Goal", FormFieldKind.Text, false, sprint?.Goal),
            Field(StartId, "Start (yyyy-MM-dd HH:mm)", FormFieldKind.DateTime, false, Local(sprint?.StartDate)),
            Field(EndId, "End (yyyy-MM-dd HH:mm)", FormFieldKind.DateTime, false, Local(sprint?.EndDate)),
        ];
    }

    /// <summary>A moment as the form shows it: local time, minutes.</summary>
    public static string? Local(DateTimeOffset? moment)
    {
        return moment?.ToLocalTime().ToString(FormValueParser.DateTimeFormat, CultureInfo.InvariantCulture);
    }

    private static FormField Field(string id, string label, FormFieldKind kind, bool required, string? text)
    {
        var field = FormField.Create(id, label, kind, required, []);

        return string.IsNullOrEmpty(text) ? field : field.WithInitial(FormValue.OfText(text));
    }
}
