using System.Text;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Reads the inline Markdown-like text of one block (<see cref="AdfMarkdownSyntax" />) into ADF nodes. Pure and
/// tolerant: it never throws, and a delimiter without its partner is plain text. Styles toggle, so they may overlap;
/// a backslash makes the next punctuation character (or space) literal. A newline is a hard break.
/// </summary>
internal static partial class AdfMarkdownInlineParser
{
    private const string AutolinkSchemeExtras = "+-.";

    public static List<AdfNode> Parse(string text)
    {
        var tokens = Tokenize(text, 0, text.Length, false);
        var runs = new RunBuilder();

        Resolve(tokens, AdfMarkdownStyle.None, null, runs);

        var nodes = runs.Finish();

        // A line break before the first or after the last character has nothing to separate.
        while (nodes.Count > 0 && nodes[^1] is AdfHardBreak)
        {
            nodes.RemoveAt(nodes.Count - 1);
        }

        while (nodes.Count > 0 && nodes[0] is AdfHardBreak)
        {
            nodes.RemoveAt(0);
        }

        return nodes;
    }

    private static List<Token> Tokenize(string text, int start, int end, bool insideLink)
    {
        var tokens = new List<Token>();
        var plain = new StringBuilder();
        var index = start;

        while (index < end)
        {
            var next = TokenizeAt(text, index, end, insideLink, tokens, plain);

            index = next;
        }

        FlushPlain(tokens, plain);

        return tokens;
    }

    private static int TokenizeAt(
        string text,
        int index,
        int end,
        bool insideLink,
        List<Token> tokens,
        StringBuilder plain)
    {
        var character = text[index];

        switch (character)
        {
            case AdfMarkdownSyntax.Escape when index + 1 < end && IsEscapable(text[index + 1]):
                plain.Append(text[index + 1]);

                return index + 2;
            case '\n':
                FlushPlain(tokens, plain);
                tokens.Add(new Token(TokenKind.Break));

                return index + 1;
            case AdfMarkdownSyntax.CodeFence:
                return TokenizeCode(text, index, end, tokens, plain);
            case AdfMarkdownSyntax.Emphasis:
                return AddDelimiter(text, index, end, tokens, plain);
            case AdfMarkdownSyntax.StrikeCharacter or AdfMarkdownSyntax.UnderlineCharacter
                when index + 1 < end && text[index + 1] == character:
                return AddDelimiter(text, index, end, tokens, plain);
            case AdfMarkdownSyntax.LinkTextStart when !insideLink:
                return TryLink(text, index, end, tokens, plain);
            case AdfMarkdownSyntax.MentionStart when !insideLink
                && index + 1 < end && text[index + 1] == AdfMarkdownSyntax.LinkTextStart:
                return TryMention(text, index, end, tokens, plain);
            case AdfMarkdownSyntax.AutolinkStart:
                return TryAutolink(text, index, end, tokens, plain);
            default:
                plain.Append(character);

                return index + 1;
        }
    }

    private static int AddDelimiter(string text, int index, int end, List<Token> tokens, StringBuilder plain)
    {
        var character = text[index];
        var isDouble = index + 1 < end && text[index + 1] == character;
        var style = character switch
        {
            AdfMarkdownSyntax.StrikeCharacter => AdfMarkdownStyle.Strike,
            AdfMarkdownSyntax.UnderlineCharacter => AdfMarkdownStyle.Underline,
            _ => isDouble ? AdfMarkdownStyle.Strong : AdfMarkdownStyle.Emphasis,
        };

        FlushPlain(tokens, plain);
        tokens.Add(new Token(TokenKind.Delimiter) { Style = style });

        return index + (isDouble ? 2 : 1);
    }

    private static int TokenizeCode(string text, int index, int end, List<Token> tokens, StringBuilder plain)
    {
        var length = RunLength(text, index, end);
        var close = FindCodeEnd(text, index + length, end, length);

        if (close < 0)
        {
            plain.Append(AdfMarkdownSyntax.CodeFence, length);

            return index + length;
        }

        var code = text.Substring(index + length, close - index - length);

        if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.AsSpan().Trim(' ').Length > 0)
        {
            code = code[1..^1];
        }

        FlushPlain(tokens, plain);
        tokens.Add(new Token(TokenKind.Code) { Text = code });

        return close + length;
    }

    private static int TryLink(string text, int index, int end, List<Token> tokens, StringBuilder plain)
    {
        var close = FindClosingBracket(text, index + 1, end);

        if (close < 0 || close + 1 >= end || text[close + 1] != AdfMarkdownSyntax.DestinationStart)
        {
            plain.Append(AdfMarkdownSyntax.LinkTextStart);

            return index + 1;
        }

        var destinationEnd = FindDestinationEnd(text, close + 2, end);
        var destination = destinationEnd < 0 ? string.Empty : Unescape(text, close + 2, destinationEnd);

        if (destination.Length == 0)
        {
            plain.Append(AdfMarkdownSyntax.LinkTextStart);

            return index + 1;
        }

        FlushPlain(tokens, plain);
        tokens.Add(
            new Token(TokenKind.Link)
            {
                Text = destination,
                Children = Tokenize(text, index + 1, close, true),
            });

        return destinationEnd + 1;
    }

    private static int TryMention(string text, int index, int end, List<Token> tokens, StringBuilder plain)
    {
        var close = FindClosingBracket(text, index + 2, end);
        var schemeStart = close + 2;

        if (close < 0
            || close + 1 >= end
            || text[close + 1] != AdfMarkdownSyntax.DestinationStart
            || !text.AsSpan(schemeStart).StartsWith(AdfMarkdownSyntax.MentionScheme, StringComparison.Ordinal))
        {
            plain.Append(AdfMarkdownSyntax.MentionStart);

            return index + 1;
        }

        var destinationEnd = FindDestinationEnd(text, schemeStart, end);

        if (destinationEnd < 0)
        {
            plain.Append(AdfMarkdownSyntax.MentionStart);

            return index + 1;
        }

        var identifier = Unescape(text, schemeStart + AdfMarkdownSyntax.MentionScheme.Length, destinationEnd);
        var name = Unescape(text, index + 2, close);

        FlushPlain(tokens, plain);
        tokens.Add(
            new Token(TokenKind.Mention)
            {
                Text = identifier,
                Name = name.Length == 0 ? null : $"{AdfMarkdownSyntax.MentionStart}{name}",
            });

        return destinationEnd + 1;
    }

    private static int TryAutolink(string text, int index, int end, List<Token> tokens, StringBuilder plain)
    {
        var colon = index + 1;

        while (colon < end && (char.IsAsciiLetterOrDigit(text[colon]) || AutolinkSchemeExtras.Contains(text[colon])))
        {
            colon++;
        }

        var close = colon;

        while (close < end && !char.IsWhiteSpace(text[close]) && text[close] != AdfMarkdownSyntax.AutolinkStart
            && text[close] != AdfMarkdownSyntax.AutolinkEnd)
        {
            close++;
        }

        if (colon - index < 3 || colon >= end || text[colon] != ':' || close >= end
            || text[close] != AdfMarkdownSyntax.AutolinkEnd || close == colon + 1)
        {
            plain.Append(AdfMarkdownSyntax.AutolinkStart);

            return index + 1;
        }

        FlushPlain(tokens, plain);
        tokens.Add(new Token(TokenKind.Card) { Text = text.Substring(index + 1, close - index - 1) });

        return close + 1;
    }

    private static void Resolve(List<Token> tokens, AdfMarkdownStyle style, string? href, RunBuilder runs)
    {
        MarkUnpairedDelimiters(tokens);

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            switch (token.Kind)
            {
                case TokenKind.Text:
                    runs.Add(token.Text, style, false, href);

                    break;
                case TokenKind.Code:
                    runs.Add(token.Text, style, true, href);

                    break;
                case TokenKind.Delimiter when token.IsLiteral:
                    runs.Add(DelimiterText(token.Style), style, false, href);

                    break;
                case TokenKind.Delimiter:
                    style ^= token.Style;

                    break;
                case TokenKind.Link:
                    Resolve(token.Children, style, token.Text, runs);

                    break;
                case TokenKind.Break:
                    runs.AddNode(new AdfHardBreak());

                    break;
                case TokenKind.Card:
                    runs.AddNode(new AdfInlineCard(token.Text));

                    break;
                case TokenKind.Mention:
                    runs.AddNode(new AdfMention(token.Text, token.Name));

                    break;
            }
        }
    }

    /// <summary>The last delimiter of a style that occurs an odd number of times has no partner.</summary>
    private static void MarkUnpairedDelimiters(List<Token> tokens)
    {
        Token? lastStrong = null;
        Token? lastEmphasis = null;
        Token? lastStrike = null;
        Token? lastUnderline = null;
        var strongOdd = false;
        var emphasisOdd = false;
        var strikeOdd = false;
        var underlineOdd = false;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (token.Kind != TokenKind.Delimiter)
            {
                continue;
            }

            switch (token.Style)
            {
                case AdfMarkdownStyle.Strong:
                    lastStrong = token;
                    strongOdd = !strongOdd;

                    break;
                case AdfMarkdownStyle.Emphasis:
                    lastEmphasis = token;
                    emphasisOdd = !emphasisOdd;

                    break;
                case AdfMarkdownStyle.Strike:
                    lastStrike = token;
                    strikeOdd = !strikeOdd;

                    break;
                default:
                    lastUnderline = token;
                    underlineOdd = !underlineOdd;

                    break;
            }
        }

        MarkLiteral(lastStrong, strongOdd);
        MarkLiteral(lastEmphasis, emphasisOdd);
        MarkLiteral(lastStrike, strikeOdd);
        MarkLiteral(lastUnderline, underlineOdd);
    }

    private static void MarkLiteral(Token? token, bool isOdd)
    {
        if (isOdd && token is not null)
        {
            token.IsLiteral = true;
        }
    }

    private static string DelimiterText(AdfMarkdownStyle style)
    {
        return style switch
        {
            AdfMarkdownStyle.Strong => AdfMarkdownSyntax.Strong,
            AdfMarkdownStyle.Strike => AdfMarkdownSyntax.Strike,
            AdfMarkdownStyle.Underline => AdfMarkdownSyntax.Underline,
            _ => AdfMarkdownSyntax.Emphasis.ToString(),
        };
    }

    private enum TokenKind
    {
        Text,
        Delimiter,
        Code,
        Link,
        Break,
        Card,
        Mention,
    }

    private sealed class Token(TokenKind kind)
    {
        public TokenKind Kind { get; } = kind;

        public string Text { get; init; } = string.Empty;

        public string? Name { get; init; }

        public AdfMarkdownStyle Style { get; init; }

        public bool IsLiteral { get; set; }

        public List<Token> Children { get; init; } = [];
    }

    /// <summary>Collects text, joining neighbours that share style, code and link.</summary>
    private sealed class RunBuilder
    {
        private readonly List<AdfNode> _nodes = [];
        private readonly StringBuilder _pending = new();
        private AdfMarkdownStyle _style;
        private bool _isCode;
        private string? _href;

        public void Add(string text, AdfMarkdownStyle style, bool isCode, string? href)
        {
            if (_pending.Length > 0 && (style != _style || isCode != _isCode || href != _href))
            {
                Flush();
            }

            _style = style;
            _isCode = isCode;
            _href = href;
            _pending.Append(text);
        }

        public void AddNode(AdfNode node)
        {
            Flush();
            _nodes.Add(node);
        }

        public List<AdfNode> Finish()
        {
            Flush();

            return _nodes;
        }

        private void Flush()
        {
            if (_pending.Length == 0)
            {
                return;
            }

            _nodes.Add(new AdfText(_pending.ToString(), AdfMarkdownStyleMarks.MarksOf(_style, _isCode, _href)));
            _pending.Clear();
        }
    }
}
