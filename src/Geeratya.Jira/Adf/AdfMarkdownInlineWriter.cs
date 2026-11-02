using System.Globalization;
using System.Text;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Writes the inline nodes of one block as Markdown-like text (<see cref="AdfMarkdownSyntax" />). Pure. Characters
/// the parser reads as syntax are escaped with a backslash; what the text cannot say (colour, sub and superscript,
/// dates, statuses) is written as its plain text, which is why <see cref="AdfRoundTrip" /> reports such documents.
/// </summary>
public static class AdfMarkdownInlineWriter
{
    public static string Write(IReadOnlyList<AdfNode> children)
    {
        var emitter = new Emitter();

        emitter.Append(AdfInlineRuns.Merge(children));

        return emitter.Finish();
    }

    /// <summary>Appends text with every character escaped that would read as syntax at the given position.</summary>
    internal static void AppendEscaped(StringBuilder builder, string text, bool atLineStart)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (index == 0 && atLineStart && EscapesAtLineStart(text))
            {
                AppendLineStartCharacter(builder, text);

                return;
            }

            if (NeedsEscape(text, index))
            {
                builder.Append(AdfMarkdownSyntax.Escape);
            }

            builder.Append(character);
        }
    }

    private static bool EscapesAtLineStart(string text)
    {
        var first = text[0];

        return first is ' ' or AdfMarkdownSyntax.HeadingMarker or AdfMarkdownSyntax.QuoteMarker
            or AdfMarkdownSyntax.RuleMarker or AdfMarkdownSyntax.UnderlineCharacter or '_'
            || IsOrderedMarker(text);
    }

    private static void AppendLineStartCharacter(StringBuilder builder, string text)
    {
        if (IsOrderedMarker(text))
        {
            var digits = 0;

            while (char.IsAsciiDigit(text[digits]))
            {
                digits++;
            }

            builder.Append(text, 0, digits);
            builder.Append(AdfMarkdownSyntax.Escape);
            builder.Append(text[digits]);
            AppendRest(builder, text, digits + 1);

            return;
        }

        builder.Append(AdfMarkdownSyntax.Escape);
        builder.Append(text[0]);
        AppendRest(builder, text, 1);
    }

    private static void AppendRest(StringBuilder builder, string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (NeedsEscape(text, index))
            {
                builder.Append(AdfMarkdownSyntax.Escape);
            }

            builder.Append(text[index]);
        }
    }

    private static bool IsOrderedMarker(string text)
    {
        var index = 0;

        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index > 0 && index < text.Length && text[index] is '.' or ')';
    }

    private static bool NeedsEscape(string text, int index)
    {
        var character = text[index];

        if (character is AdfMarkdownSyntax.Escape
            or AdfMarkdownSyntax.Emphasis
            or AdfMarkdownSyntax.CodeFence
            or AdfMarkdownSyntax.LinkTextStart
            or AdfMarkdownSyntax.LinkTextEnd
            or AdfMarkdownSyntax.AutolinkStart)
        {
            return true;
        }

        if (character is AdfMarkdownSyntax.StrikeCharacter or AdfMarkdownSyntax.UnderlineCharacter)
        {
            return (index > 0 && text[index - 1] == character)
                || (index + 1 < text.Length && text[index + 1] == character);
        }

        return false;
    }

    private sealed class Emitter
    {
        private readonly StringBuilder _builder = new();
        private AdfMarkdownStyle _style = AdfMarkdownStyle.None;
        private AdfMarkdownStyle _styleBeforeLink;
        private string? _linkHref;

        public void Append(List<AdfNode> nodes)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] is AdfText text)
                {
                    AppendText(text, nodes, index);
                }
                else
                {
                    CloseLink();
                    Transition(AdfMarkdownStyle.None);
                    AppendNode(nodes[index]);
                }
            }
        }

        public string Finish()
        {
            CloseLink();
            Transition(AdfMarkdownStyle.None);

            return _builder.ToString();
        }

        private void AppendText(AdfText text, List<AdfNode> nodes, int index)
        {
            var href = FindHref(text.Marks);

            if (href != _linkHref)
            {
                CloseLink();

                if (href is not null)
                {
                    OpenLink(href, nodes, index);
                }
            }

            Transition(AdfMarkdownStyleMarks.StyleOf(text.Marks));

            if (HasCode(text.Marks))
            {
                AppendCode(text.Text);

                return;
            }

            if (text.Text[0] == ' ' && IsLoneStarLine())
            {
                // A star and a space at the start of a line would read as a bullet.
                _builder.Append(AdfMarkdownSyntax.Escape);
            }

            AppendEscaped(_builder, text.Text, AtLineStart());
        }

        private void AppendNode(AdfNode node)
        {
            switch (node)
            {
                case AdfHardBreak:
                    _builder.Append('\n');

                    break;
                case AdfInlineCard { Url: { } url }:
                    _builder.Append(AdfMarkdownSyntax.AutolinkStart).Append(url).Append(AdfMarkdownSyntax.AutolinkEnd);

                    break;
                case AdfMention mention:
                    AppendMention(mention);

                    break;
                default:
                    AppendEscaped(_builder, PlainText(node), AtLineStart());

                    break;
            }
        }

        private void AppendMention(AdfMention mention)
        {
            var name = mention.Text ?? string.Empty;

            if (name.StartsWith(AdfMarkdownSyntax.MentionStart))
            {
                name = name[1..];
            }

            _builder.Append(AdfMarkdownSyntax.MentionStart).Append(AdfMarkdownSyntax.LinkTextStart);
            AppendEscaped(_builder, name, false);
            _builder.Append(AdfMarkdownSyntax.LinkTextEnd).Append(AdfMarkdownSyntax.DestinationStart);
            _builder.Append(AdfMarkdownSyntax.MentionScheme);
            AppendDestination(mention.Id ?? string.Empty);
            _builder.Append(AdfMarkdownSyntax.DestinationEnd);
        }

        private void AppendCode(string text)
        {
            var longest = 0;
            var run = 0;

            for (var index = 0; index < text.Length; index++)
            {
                run = text[index] == AdfMarkdownSyntax.CodeFence ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }

            var fence = new string(AdfMarkdownSyntax.CodeFence, longest + 1);
            var pad = text.Length == 0
                || text[0] == AdfMarkdownSyntax.CodeFence
                || text[^1] == AdfMarkdownSyntax.CodeFence
                || (text[0] == ' ' && text[^1] == ' ' && text.AsSpan().Trim(' ').Length > 0);

            _builder.Append(fence);

            if (pad)
            {
                _builder.Append(' ');
            }

            _builder.Append(text);

            if (pad)
            {
                _builder.Append(' ');
            }

            _builder.Append(fence);
        }

        /// <summary>
        /// Keeps the styles every run of the link shares open around the brackets (the parser carries them into the
        /// link text) and closes the others first: a style toggled inside the text must also end inside it.
        /// </summary>
        private void OpenLink(string href, List<AdfNode> nodes, int index)
        {
            var common = AdfMarkdownStyleMarks.StyleOf(((AdfText)nodes[index]).Marks);

            for (var next = index + 1; next < nodes.Count && nodes[next] is AdfText following; next++)
            {
                if (FindHref(following.Marks) != href)
                {
                    break;
                }

                common &= AdfMarkdownStyleMarks.StyleOf(following.Marks);
            }

            Transition(_style & common);
            _styleBeforeLink = _style;
            _builder.Append(AdfMarkdownSyntax.LinkTextStart);
            _linkHref = href;
        }

        private void CloseLink()
        {
            if (_linkHref is null)
            {
                return;
            }

            Transition(_styleBeforeLink);

            _builder.Append(AdfMarkdownSyntax.LinkTextEnd).Append(AdfMarkdownSyntax.DestinationStart);
            AppendDestination(_linkHref);
            _builder.Append(AdfMarkdownSyntax.DestinationEnd);
            _linkHref = null;
        }

        private void AppendDestination(string destination)
        {
            for (var index = 0; index < destination.Length; index++)
            {
                var character = destination[index];

                if (character is AdfMarkdownSyntax.Escape
                    or AdfMarkdownSyntax.DestinationStart
                    or AdfMarkdownSyntax.DestinationEnd)
                {
                    _builder.Append(AdfMarkdownSyntax.Escape);
                }

                _builder.Append(character);
            }
        }

        private void Transition(AdfMarkdownStyle target)
        {
            var closing = _style & ~target;
            var opening = target & ~_style;

            AppendDelimiter(closing, AdfMarkdownStyle.Underline, AdfMarkdownSyntax.Underline);
            AppendDelimiter(closing, AdfMarkdownStyle.Strike, AdfMarkdownSyntax.Strike);
            AppendDelimiter(closing, AdfMarkdownStyle.Emphasis, AdfMarkdownSyntax.Emphasis.ToString());
            AppendDelimiter(closing, AdfMarkdownStyle.Strong, AdfMarkdownSyntax.Strong);
            AppendDelimiter(opening, AdfMarkdownStyle.Strong, AdfMarkdownSyntax.Strong);
            AppendDelimiter(opening, AdfMarkdownStyle.Emphasis, AdfMarkdownSyntax.Emphasis.ToString());
            AppendDelimiter(opening, AdfMarkdownStyle.Strike, AdfMarkdownSyntax.Strike);
            AppendDelimiter(opening, AdfMarkdownStyle.Underline, AdfMarkdownSyntax.Underline);
            _style = target;
        }

        private void AppendDelimiter(AdfMarkdownStyle changing, AdfMarkdownStyle style, string delimiter)
        {
            if ((changing & style) != 0)
            {
                _builder.Append(delimiter);
            }
        }

        private bool IsLoneStarLine()
        {
            var length = _builder.Length;

            return length > 0
                && _builder[length - 1] == AdfMarkdownSyntax.Emphasis
                && (length == 1 || _builder[length - 2] == '\n');
        }

        private bool AtLineStart()
        {
            return _builder.Length == 0 || _builder[^1] == '\n';
        }

        private static string? FindHref(IReadOnlyList<AdfMark> marks)
        {
            for (var index = 0; index < marks.Count; index++)
            {
                if (marks[index] is AdfLinkMark { Href: { Length: > 0 } href })
                {
                    return href;
                }
            }

            return null;
        }

        private static bool HasCode(IReadOnlyList<AdfMark> marks)
        {
            for (var index = 0; index < marks.Count; index++)
            {
                if (marks[index] is AdfCodeMark)
                {
                    return true;
                }
            }

            return false;
        }

        private static string PlainText(AdfNode node)
        {
            return node switch
            {
                AdfEmoji emoji => emoji.Text ?? emoji.ShortName ?? string.Empty,
                AdfStatus status => status.Text,
                AdfDate { TimestampMilliseconds: { } milliseconds } => DateTimeOffset
                    .FromUnixTimeMilliseconds(milliseconds)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => string.Empty,
            };
        }
    }
}
