using System.Globalization;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The two quick filters the recorded boards define (<c>assignee = currentUser()</c> and
/// <c>updatedDate &gt;= -1d</c>) evaluated over issue JSON, so a board request that carries them in <c>jql</c>
/// answers the narrowed list. Any other clause is ignored, like every other JQL form the fake does not model.
/// </summary>
public static class FakeQuickFilters
{
    private const string AssigneeClause = "assignee = currentUser()";
    private const string UpdatedClause = "updatedDate >= -1d";
    private const string FieldsName = "fields";

    /// <summary>True when the issue passes every clause named in <paramref name="jql"/>.</summary>
    public static bool Matches(JsonNode issue, string jql, string accountId, DateTimeOffset now)
    {
        var fields = issue[FieldsName];

        if (jql.Contains(AssigneeClause, StringComparison.OrdinalIgnoreCase)
            && fields?["assignee"]?["accountId"]?.GetValue<string>() != accountId)
        {
            return false;
        }

        if (jql.Contains(UpdatedClause, StringComparison.OrdinalIgnoreCase))
        {
            var text = fields?["updated"]?.GetValue<string>();
            var parsed = DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var updated);

            return parsed && updated >= now.AddDays(-1);
        }

        return true;
    }

    /// <summary>True when <paramref name="jql"/> holds a clause this class evaluates.</summary>
    public static bool Applies(string jql)
    {
        return jql.Contains(AssigneeClause, StringComparison.OrdinalIgnoreCase)
            || jql.Contains(UpdatedClause, StringComparison.OrdinalIgnoreCase);
    }
}
