using System.Globalization;
using System.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Jql;

/// <summary>
/// The one place JQL the app builds comes from. Every value is quoted and escaped here, never concatenated by the
/// caller. Pure: same input, same output. JQL typed by the user does not pass through it (it is sent as typed).
/// </summary>
public static class JqlBuilder
{
    public const string RankField = "Rank";
    public const string CreatedField = "created";
    public const string UpdatedField = "updated";
    public const string EpicIssueType = "Epic";
    private const int EpicHierarchyLevel = 1;
    private const int StandardHierarchyLevel = 0;

    /// <summary>Jira's status category keys, as <c>statusCategory</c> accepts them. Keys are language independent; the names ("To Do") are localized per site, so the builder never uses them.</summary>
    public const string StatusCategoryToDo = StatusCategoryKeys.New;
    public const string StatusCategoryInProgress = StatusCategoryKeys.Indeterminate;
    public const string StatusCategoryDone = StatusCategoryKeys.Done;

    private const string AndOperator = " AND ";
    private const string OrOperator = " OR ";
    private const string SubTaskTypes = "issuetype in subTaskIssueTypes()";
    private const string OrderByKeyword = "ORDER BY";
    private const string OrderWord = "order";
    private const string ByWord = "by";
    private const char DoubleQuote = '"';
    private const char SingleQuote = '\'';
    private const char Backslash = '\\';
    private const char LineSeparator = '\u2028';
    private const char ParagraphSeparator = '\u2029';

    /// <summary>A quoted JQL string literal: backslash, quote and control characters are escaped.</summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append(DoubleQuote);

        foreach (var character in value)
        {
            AppendEscaped(builder, character);
        }

        builder.Append(DoubleQuote);

        return builder.ToString();
    }

    /// <summary><c>parent = KEY</c>: the direct children of an issue or epic, in both project kinds.</summary>
    public static string Parent(string issueKey) => $"parent = {KeyOrQuoted(issueKey)}";

    /// <summary>
    /// <c>parent = KEY AND issuetype in subTaskIssueTypes()</c>: the sub-tasks of an issue (not the children of an
    /// epic), which Jira deletes with it when <c>deleteSubtasks</c> is set.
    /// </summary>
    public static string SubTasksOf(string issueKey) => $"{Parent(issueKey)}{AndOperator}{SubTaskTypes}";

    /// <summary><c>parent in (A-1, A-2)</c>: one level of children for several parents.</summary>
    public static string ParentIn(IEnumerable<string> issueKeys) => $"parent in ({KeyList(issueKeys)})";

    /// <summary><c>filter = 10001</c> for a numeric filter id; anything else is quoted (a filter name).</summary>
    public static string Filter(string filterIdOrName) => $"filter = {IdOrQuoted(filterIdOrName)}";

    /// <summary><c>key in (A-1, A-2)</c>. The list must not be empty: Jira has no valid empty <c>in ()</c>.</summary>
    public static string KeyIn(IEnumerable<string> issueKeys) => $"key in ({KeyList(issueKeys)})";

    /// <summary><c>project = "KEY"</c>, always quoted so a key that is a JQL reserved word still works.</summary>
    public static string Project(string projectKey) => $"project = {Quote(projectKey)}";

    /// <summary>Issues the current account reported in the last ten minutes (the check of a lost create).</summary>
    public static string RecentOwnIssues() => OwnIssuesSince(10);

    /// <summary>Issues the current account reported in the last <paramref name="minutes"/> minutes (at least one).</summary>
    public static string OwnIssuesSince(int minutes) =>
        $"reporter = currentUser() AND created >= -{Math.Max(1, minutes).ToString(CultureInfo.InvariantCulture)}m";

    /// <summary>
    /// The scope of a parent search for a hierarchy level: epics for level 1, standard issue types for level 0 (the
    /// parent of a sub-task); other levels are not narrowed here. Applied before Jira caps the list.
    /// </summary>
    public static string ParentScope(string projectKey, int? level)
    {
        var project = Project(projectKey);

        return level switch
        {
            EpicHierarchyLevel => $"{project}{AndOperator}{IssueTypeEpic()}",
            StandardHierarchyLevel => $"{project}{AndOperator}issuetype in standardIssueTypes()",
            _ => project,
        };
    }

    public static string IssueTypeEpic() => IssueType(EpicIssueType);

    public static string IssueType(string name) => $"issuetype = {Quote(name)}";

    /// <summary><c>statusCategory = "indeterminate"</c>.</summary>
    public static string StatusCategory(string category) => $"statusCategory = {Quote(category)}";

    /// <summary>Joins clauses with <c>AND</c>, skipping empty ones. Use <see cref="Or"/> for an OR clause.</summary>
    public static string And(params IEnumerable<string> clauses) => Join(clauses, AndOperator);

    /// <summary>Joins clauses with <c>OR</c> in parentheses so it composes safely under <see cref="And"/>.</summary>
    public static string Or(params IEnumerable<string> clauses)
    {
        var joined = Join(clauses, OrOperator);

        return joined.Length == 0 ? string.Empty : $"({joined})";
    }

    /// <summary>Appends <c>ORDER BY</c> terms to a restriction that has none (empty restriction: the result is unbounded).</summary>
    public static string WithOrder(string restriction, params IEnumerable<JqlOrder> orders)
    {
        var terms = string.Join(", ", orders.Select(Term));
        var body = restriction.Trim();

        if (terms.Length == 0)
        {
            return body;
        }

        if (body.Length == 0)
        {
            return $"{OrderByKeyword} {terms}";
        }

        return $"{body} {OrderByKeyword} {terms}";
    }

    /// <summary>
    /// True when the JQL has a search restriction before any <c>ORDER BY</c>. Jira answers 400 for empty or
    /// order-only JQL (<c>docs/internal/jira-layer.md</c>, "JQL search"). Quoted text is skipped, so an <c>order by</c>
    /// inside a string does not count.
    /// </summary>
    public static bool IsBounded(string? jql)
    {
        if (string.IsNullOrWhiteSpace(jql))
        {
            return false;
        }

        var orderIndex = FindOrderBy(jql);
        var restriction = orderIndex < 0 ? jql : jql[..orderIndex];

        return !string.IsNullOrWhiteSpace(restriction);
    }

    /// <summary>
    /// For app-built queries only: returns the JQL unchanged when it is bounded, else puts the restriction in front
    /// of its <c>ORDER BY</c> (or alone). Never call it on JQL the user typed; that is sent as typed.
    /// </summary>
    public static string EnsureBounded(string jql, string restriction)
    {
        if (IsBounded(jql))
        {
            return jql;
        }

        var orderIndex = FindOrderBy(jql);

        return orderIndex < 0 ? restriction : $"{restriction} {jql[orderIndex..].TrimEnd()}";
    }

    private static string Term(JqlOrder order) => $"{order.Field} {(order.Descending ? "DESC" : "ASC")}";

    private static string Join(IEnumerable<string> clauses, string separator) =>
        string.Join(separator, clauses.Where(clause => !string.IsNullOrWhiteSpace(clause)));

    private static string KeyList(IEnumerable<string> issueKeys)
    {
        var list = string.Join(", ", issueKeys.Select(KeyOrQuoted));

        if (list.Length == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(issueKeys));
        }

        return list;
    }

    private static string KeyOrQuoted(string value) => IsIssueKey(value) ? value : Quote(value);

    private static string IdOrQuoted(string value) => IsDigits(value) ? value : Quote(value);

    /// <summary>An issue key is <c>PROJECT-123</c>: upper-case project key, dash, digits.</summary>
    private static bool IsIssueKey(string value)
    {
        var dash = value.LastIndexOf('-');

        if (dash <= 0 || !IsDigits(value[(dash + 1)..]) || !char.IsAsciiLetterUpper(value[0]))
        {
            return false;
        }

        return value[..dash].All(IsProjectKeyCharacter);
    }

    private static bool IsProjectKeyCharacter(char character) =>
        char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_';

    private static bool IsDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static void AppendEscaped(StringBuilder builder, char character)
    {
        switch (character)
        {
            case DoubleQuote:
            case Backslash:
                builder.Append(Backslash).Append(character);
                break;
            case '\n':
                builder.Append("\\n");
                break;
            case '\r':
                builder.Append("\\r");
                break;
            case '\t':
                builder.Append("\\t");
                break;
            default:
                AppendPlain(builder, character);
                break;
        }
    }

    private static void AppendPlain(StringBuilder builder, char character)
    {
        if (char.IsControl(character) || character is LineSeparator or ParagraphSeparator)
        {
            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));

            return;
        }

        builder.Append(character);
    }

    /// <summary>Index of the first <c>ORDER BY</c> outside quotes, or -1.</summary>
    private static int FindOrderBy(string jql)
    {
        for (var index = 0; index < jql.Length; index++)
        {
            var character = jql[index];

            if (character is DoubleQuote or SingleQuote)
            {
                index = ClosingQuote(jql, index);

                continue;
            }

            if (StartsOrderBy(jql, index))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Index of the quote closing the one at <paramref name="start"/> (the end when unterminated).</summary>
    private static int ClosingQuote(string jql, int start)
    {
        for (var position = start + 1; position < jql.Length; position++)
        {
            if (jql[position] == Backslash)
            {
                position++;

                continue;
            }

            if (jql[position] == jql[start])
            {
                return position;
            }
        }

        return jql.Length;
    }

    private static bool StartsOrderBy(string jql, int index)
    {
        if (index > 0 && !IsBoundary(jql[index - 1]))
        {
            return false;
        }

        if (!MatchesWord(jql, index, OrderWord))
        {
            return false;
        }

        var afterWord = index + OrderWord.Length;
        var byIndex = afterWord;

        while (byIndex < jql.Length && char.IsWhiteSpace(jql[byIndex]))
        {
            byIndex++;
        }

        return byIndex > afterWord && MatchesWord(jql, byIndex, ByWord);
    }

    /// <summary>The word at the index, case-insensitive, and not followed by more identifier characters.</summary>
    private static bool MatchesWord(string jql, int index, string word)
    {
        var end = index + word.Length;

        var comparison = StringComparison.OrdinalIgnoreCase;

        if (end > jql.Length || string.Compare(jql, index, word, 0, word.Length, comparison) != 0)
        {
            return false;
        }

        return end == jql.Length || IsBoundary(jql[end]);
    }

    private static bool IsBoundary(char character) => !char.IsLetterOrDigit(character) && character != '_';
}
