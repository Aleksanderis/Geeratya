using System.Text;

namespace Geeratya.Jira.Adf;

/// <summary>
/// The atoms of the editor text: a mention <c>@[Name](mention:id)</c> and a reference to an issue
/// <c>[KEY](https://site/browse/KEY)</c>. The editor inserts them whole and moves over and deletes them as one
/// character; the parser turns the first into an ADF <c>mention</c> node and the second into a link mark.
/// </summary>
public static class AdfMarkdownTokens
{
    private const string BrowseSegment = "/browse/";
    private const string MentionOpen = "(mention:";

    /// <summary>The editor text of a mention of <paramref name="accountId"/>, written as <paramref name="name"/>.</summary>
    public static string Mention(string accountId, string name)
    {
        return Written(new AdfMention(accountId, $"{AdfMarkdownSyntax.MentionStart}{name}"));
    }

    /// <summary>The editor text of a link to <paramref name="key"/> on the site (the bare key when either is invalid).</summary>
    public static string IssueLink(string siteUrl, string key)
    {
        if (!IsIssueKey(key) || !Uri.TryCreate(siteUrl, UriKind.Absolute, out var site))
        {
            return key;
        }

        // Only scheme and host of the site, as IssueUrls.Browse builds the page address.
        var href = $"{site.GetLeftPart(UriPartial.Authority)}{BrowseSegment}{key}";

        return Written(new AdfText(key, [new AdfLinkMark(href)]));
    }

    /// <summary>
    /// The short form the editor shows for <paramref name="span"/>: <c>@Name</c> for a mention, the key for an issue
    /// link (escapes removed). The saved text is not changed.
    /// </summary>
    public static string Label(string text, AdfTokenSpan span)
    {
        var isMention = text[span.Start] == AdfMarkdownSyntax.MentionStart;
        var from = span.Start + (isMention ? 2 : 1);
        var close = Closing(text, from, AdfMarkdownSyntax.LinkTextEnd);

        if (close < 0)
        {
            return text[span.Start..span.End];
        }

        var label = new StringBuilder(isMention ? "@" : string.Empty);

        for (var index = from; index < close; index++)
        {
            if (text[index] == AdfMarkdownSyntax.Escape && index + 1 < close)
            {
                index++;
            }

            label.Append(text[index]);
        }

        return label.ToString();
    }

    /// <summary>The spans of every token in <paramref name="text"/>, in order.</summary>
    public static IReadOnlyList<AdfTokenSpan> Find(string text)
    {
        List<AdfTokenSpan>? spans = null;
        var index = 0;

        while (index < text.Length)
        {
            var character = text[index];

            if (character == AdfMarkdownSyntax.Escape)
            {
                index += 2;

                continue;
            }

            var end = character switch
            {
                AdfMarkdownSyntax.MentionStart => MentionEnd(text, index),
                AdfMarkdownSyntax.LinkTextStart => IssueLinkEnd(text, index),
                _ => -1,
            };

            if (end < 0)
            {
                index++;

                continue;
            }

            (spans ??= []).Add(new AdfTokenSpan(index, end));
            index = end;
        }

        return spans ?? [];
    }

    private static string Written(AdfNode inline)
    {
        return AdfMarkdownWriter.ToText(new AdfDocument(1, [new AdfParagraph([inline])], false)).TrimEnd('\n');
    }

    // @[Name](mention:id) -> the index after the closing parenthesis, or -1.
    private static int MentionEnd(string text, int start)
    {
        if (start + 1 >= text.Length || text[start + 1] != AdfMarkdownSyntax.LinkTextStart)
        {
            return -1;
        }

        var close = Closing(text, start + 2, AdfMarkdownSyntax.LinkTextEnd);

        if (close < 0 || !text.AsSpan(close + 1).StartsWith(MentionOpen, StringComparison.Ordinal))
        {
            return -1;
        }

        var end = Closing(text, close + 2, AdfMarkdownSyntax.DestinationEnd);

        return end < 0 ? -1 : end + 1;
    }

    // [KEY](https://site/browse/KEY) -> the index after the closing parenthesis, or -1.
    private static int IssueLinkEnd(string text, int start)
    {
        var close = Closing(text, start + 1, AdfMarkdownSyntax.LinkTextEnd);

        if (close < 0 || close + 1 >= text.Length || text[close + 1] != AdfMarkdownSyntax.DestinationStart)
        {
            return -1;
        }

        var end = Closing(text, close + 2, AdfMarkdownSyntax.DestinationEnd);

        if (end < 0)
        {
            return -1;
        }

        var key = text.Substring(start + 1, close - start - 1);
        var href = text.Substring(close + 2, end - close - 2);

        return IsIssueKey(key)
            && href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            && href.EndsWith($"{BrowseSegment}{key}", StringComparison.Ordinal)
            ? end + 1
            : -1;
    }

    // Index of the first unescaped closing character on the same line, or -1.
    private static int Closing(string text, int from, char closing)
    {
        for (var index = from; index < text.Length; index++)
        {
            var character = text[index];

            if (character == '\n')
            {
                return -1;
            }

            if (character == AdfMarkdownSyntax.Escape)
            {
                // A backslash before a line break does not join the lines into one token.
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    return -1;
                }

                index++;
            }
            else if (character == closing)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsIssueKey(string key)
    {
        var dash = key.LastIndexOf('-');

        if (dash < 1 || dash == key.Length - 1 || !char.IsAsciiLetter(key[0]))
        {
            return false;
        }

        return key[(dash + 1)..].All(char.IsAsciiDigit)
            && key[..dash].All(static letter => char.IsAsciiLetterOrDigit(letter) || letter == '_');
    }
}
