using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Links;

/// <summary>
/// The id of one choice in the link type field. A link type is picked together with the side the issue is on, so each
/// side is its own choice (<c>blocks</c>, <c>is blocked by</c>); the id carries the type id, the side and the wording,
/// because a form field keeps nothing but ids. Pure.
/// </summary>
public static class LinkOptionId
{
    private const char Separator = '/';
    private const string OutwardCode = "out";
    private const string InwardCode = "in";

    public static string Of(string typeId, IssueLinkDirection direction, string text)
    {
        var code = direction == IssueLinkDirection.Outward ? OutwardCode : InwardCode;

        return $"{typeId}{Separator}{code}{Separator}{text}";
    }

    /// <summary>Reads an id made by <see cref="Of"/>; false for anything else.</summary>
    public static bool TryParse(string id, out LinkChoice choice)
    {
        choice = new LinkChoice(string.Empty, IssueLinkDirection.Outward, string.Empty);

        var parts = id.Split(Separator, 3);

        if (parts.Length != 3 || parts[0].Length == 0 || parts[1] is not (OutwardCode or InwardCode))
        {
            return false;
        }

        var direction = parts[1] == OutwardCode ? IssueLinkDirection.Outward : IssueLinkDirection.Inward;

        choice = new LinkChoice(parts[0], direction, parts[2]);

        return true;
    }
}
