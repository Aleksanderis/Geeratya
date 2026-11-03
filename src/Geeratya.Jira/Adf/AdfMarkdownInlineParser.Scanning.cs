using System.Text;

namespace Geeratya.Jira.Adf;

internal static partial class AdfMarkdownInlineParser
{
    private static int FindClosingBracket(string text, int start, int end)
    {
        var depth = 1;
        var index = start;

        while (index < end)
        {
            var character = text[index];

            if (character == AdfMarkdownSyntax.Escape)
            {
                index += 2;

                continue;
            }

            if (character == AdfMarkdownSyntax.CodeFence)
            {
                var length = RunLength(text, index, end);
                var close = FindCodeEnd(text, index + length, end, length);

                index = close < 0 ? index + length : close + length;

                continue;
            }

            if (character == AdfMarkdownSyntax.LinkTextStart)
            {
                depth++;
            }
            else if (character == AdfMarkdownSyntax.LinkTextEnd && --depth == 0)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>Finds the closing parenthesis; balanced unescaped pairs inside belong to the address.</summary>
    private static int FindDestinationEnd(string text, int start, int end)
    {
        var index = start;
        var depth = 0;

        while (index < end)
        {
            if (text[index] == '\n')
            {
                return -1;
            }

            if (text[index] == AdfMarkdownSyntax.Escape)
            {
                index += 2;

                continue;
            }

            if (text[index] == AdfMarkdownSyntax.DestinationStart)
            {
                depth++;
            }
            else if (text[index] == AdfMarkdownSyntax.DestinationEnd && depth-- == 0)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>
    /// Finds a backtick run of exactly <paramref name="length" /> characters on the same line; -1 when there is none,
    /// so code never holds a line break (a line starting with a fence would read as a code block).
    /// </summary>
    private static int FindCodeEnd(string text, int start, int end, int length)
    {
        var index = start;

        while (index < end)
        {
            if (text[index] == '\n')
            {
                return -1;
            }

            if (text[index] != AdfMarkdownSyntax.CodeFence)
            {
                index++;

                continue;
            }

            var run = RunLength(text, index, end);

            if (run == length)
            {
                return index;
            }

            index += run;
        }

        return -1;
    }

    private static int RunLength(string text, int start, int end)
    {
        var index = start;

        while (index < end && text[index] == text[start])
        {
            index++;
        }

        return index - start;
    }

    private static string Unescape(string text, int start, int end)
    {
        var builder = new StringBuilder(end - start);

        for (var index = start; index < end; index++)
        {
            if (text[index] == AdfMarkdownSyntax.Escape && index + 1 < end && IsEscapable(text[index + 1]))
            {
                index++;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    private static bool IsEscapable(char character)
    {
        return character == ' ' || (character is > ' ' and < '\u007f' && !char.IsAsciiLetterOrDigit(character));
    }

    private static void FlushPlain(List<Token> tokens, StringBuilder plain)
    {
        if (plain.Length == 0)
        {
            return;
        }

        tokens.Add(new Token(TokenKind.Text) { Text = plain.ToString() });
        plain.Clear();
    }
}
