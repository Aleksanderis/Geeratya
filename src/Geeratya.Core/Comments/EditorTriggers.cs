using Geeratya.Core.Editing;
using Geeratya.Core.Suggestions;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Comments;

/// <summary>
/// Finds the suggestion trigger before the cursor (<c>docs/ux-workflows.md</c>, "Suggestions"): <c>@</c> then a name
/// opens people, <c>#</c> then a project key (<c>#GCM</c>, then <c>#GCM-12</c>) opens issues. The character must start
/// a word, so an e-mail address or a heading marker never triggers. Pure.
/// </summary>
public static class EditorTriggers
{
    private const char MentionMarker = '@';
    private const char IssueMarker = '#';
    private const string OpeningPunctuation = "([{\"'";

    public static EditorTrigger? Find(TextBuffer buffer)
    {
        var text = buffer.Text;
        var cursor = Math.Clamp(buffer.Cursor, 0, text.Length);
        var start = cursor;

        while (start > 0 && IsQueryCharacter(text[start - 1]))
        {
            start--;
        }

        if (start == 0 || !StartsWord(text, start - 1))
        {
            return null;
        }

        var marker = text[start - 1];
        var query = text[start..cursor];

        if (InsideToken(text, start - 1))
        {
            return null;
        }

        return marker switch
        {
            MentionMarker => new EditorTrigger(SuggestionKind.IssueUsers, start - 1, query),
            IssueMarker when IsProjectKeyPrefix(query) => new EditorTrigger(SuggestionKind.Issues, start - 1, query),
            _ => null,
        };
    }

    private static bool IsQueryCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '.' or '_' or '-';
    }

    private static bool StartsWord(string text, int marker)
    {
        if (text[marker] is not (MentionMarker or IssueMarker))
        {
            return false;
        }

        return marker == 0 || char.IsWhiteSpace(text[marker - 1]) || OpeningPunctuation.Contains(text[marker - 1]);
    }

    private static bool InsideToken(string text, int index)
    {
        return AdfMarkdownTokens.Find(text).Any(span => span.Start < index && index < span.End);
    }

    // A project key (letters, digits, underscores, starting with a letter), then optionally a dash and digits.
    private static bool IsProjectKeyPrefix(string query)
    {
        if (query.Length == 0 || !char.IsAsciiLetter(query[0]))
        {
            return false;
        }

        var dash = query.IndexOf('-');
        var key = dash < 0 ? query : query[..dash];

        if (!key.All(static value => char.IsAsciiLetterOrDigit(value) || value == '_'))
        {
            return false;
        }

        return dash < 0 || query[(dash + 1)..].All(char.IsAsciiDigit);
    }
}
