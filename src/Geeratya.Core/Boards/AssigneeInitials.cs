using System.Globalization;

namespace Geeratya.Core.Boards;

/// <summary>The initials shown on a card for the assignee.</summary>
public static class AssigneeInitials
{
    private static readonly char[] _separators = [' ', '\t', '-'];

    /// <summary>
    /// The first letter of the first and of the last word of <paramref name="name"/>, upper case; one letter for a
    /// single word, empty for a blank name. A letter is a whole text element, so a surrogate pair is never split.
    /// </summary>
    public static string Of(string name)
    {
        var words = name.Split(_separators, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return string.Empty;
        }

        var first = FirstElement(words[0]);

        return words.Length == 1 ? first : $"{first}{FirstElement(words[^1])}";
    }

    private static string FirstElement(string word)
    {
        var length = StringInfo.GetNextTextElementLength(word);

        return word[..length].ToUpperInvariant();
    }
}
