using System.Text.RegularExpressions;
using Geeratya.Jira.Jql;

namespace Geeratya.Core.Sources;

/// <summary>
/// Turns the text typed in the picker's Query row into JQL: a text that already has a JQL operator or
/// <c>ORDER BY</c> is sent as typed, anything else (a bare word like <c>gcm</c>) becomes a text search.
/// </summary>
public static partial class JqlText
{
    private const string TextField = "text";
    private const string ContainsOperator = "~";

    public static string FromTyped(string text)
    {
        var trimmed = text.Trim();

        return LooksLikeJql(trimmed) ? trimmed : $"{TextField} {ContainsOperator} {JqlBuilder.Quote(trimmed)}";
    }

    /// <summary>
    /// True when the text has a comparison, <c>in (</c>, <c>is [not] empty</c>, a history operator or <c>order by</c>.
    /// </summary>
    public static bool LooksLikeJql(string text)
    {
        return OperatorPattern().IsMatch(text);
    }

    [GeneratedRegex(
        """[=<>~!]|\b(not\s+)?in\s*\(|\bis\s+(not\s+)?(empty|null)\b|\border\s+by\b|\b(was|changed)\s+(not\s+)?(from|to|by|after|before|on|during|in)\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperatorPattern();
}
