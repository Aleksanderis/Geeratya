using System.Globalization;
using System.Text;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

/// <summary>
/// Flattens inline ADF nodes (text with marks, mentions, emoji, dates, status chips, cards) into wrapped styled text.
/// A link whose text differs from its address is followed by the address in angle brackets, once per link even when
/// the link text is split over several text nodes.
/// </summary>
internal sealed class AdfInlineWriter
{
    private const string SubscriptPrefix = "_";
    private const string SuperscriptPrefix = "^";
    private const string SubscriptKind = "sub";
    private const string SuperscriptKind = "sup";
    private const string StatusColorGreen = "green";
    private const string StatusColorBlue = "blue";
    private const string StatusColorRed = "red";
    private const string DateFormat = "yyyy-MM-dd";
    private const string MentionPrefix = "@";
    private const string UnknownMention = "@unknown";
    private const string EmojiDelimiter = ":";

    private static readonly long _minTimestamp = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    private static readonly long _maxTimestamp = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    private readonly WrappedText _text;
    private readonly TextRole _baseRole;
    private readonly StringBuilder _linkText = new();

    private string? _linkAddress;

    public AdfInlineWriter(int width, TextRole baseRole)
    {
        _text = new WrappedText(width, false);
        _baseRole = baseRole;
    }

    public void Write(AdfNode node)
    {
        if (node is AdfText text)
        {
            WriteText(text);

            return;
        }

        EndLink();

        switch (node)
        {
            case AdfHardBreak:
                _text.LineBreak();
                break;
            case AdfInlineCard card:
                _text.Append(string.IsNullOrEmpty(card.Url) ? AdfMarkers.Link : card.Url, TextRole.Link);
                break;
            case AdfEmoji emoji:
                _text.Append(EmojiText(emoji), _baseRole);
                break;
            case AdfDate date:
                _text.Append(DateText(date), _baseRole);
                break;
            case AdfStatus status:
                _text.Append($"[{status.Text.ToUpperInvariant()}]", StatusRole(status));
                break;
            case AdfMention mention:
                _text.Append(MentionText(mention), TextRole.Strong);
                break;
            case AdfMedia media:
                _text.Append(MediaMarker(media, AdfMarkers.File), TextRole.Muted);
                break;
            case AdfUnknownNode unknown:
                WriteUnknown(unknown);
                break;
            case AdfContainerNode container:
                WriteChildren(container.Children);
                _text.LineBreak();
                break;
        }
    }

    /// <summary>Writes a standalone marker such as <c>[image]</c>.</summary>
    public void WriteMarker(string marker)
    {
        EndLink();
        _text.Append(marker, TextRole.Muted);
    }

    public void LineBreak()
    {
        EndLink();
        _text.LineBreak();
    }

    public void Finish(List<StyledLine> output)
    {
        EndLink();
        _text.Finish(output);
    }

    public static string MediaMarker(AdfMedia media, string marker)
    {
        return string.IsNullOrWhiteSpace(media.Alt) ? marker : $"{marker} {media.Alt}";
    }

    private void WriteChildren(IReadOnlyList<AdfNode> children)
    {
        for (var index = 0; index < children.Count; index++)
        {
            Write(children[index]);
        }
    }

    private void WriteUnknown(AdfUnknownNode unknown)
    {
        _text.Append(AdfMarkers.Unsupported(unknown.TypeName), TextRole.Muted);

        if (!string.IsNullOrEmpty(unknown.Text))
        {
            _text.Append($" {unknown.Text}", _baseRole);
        }

        WriteChildren(unknown.Children);
    }

    private void WriteText(AdfText text)
    {
        var address = LinkAddress(text);

        if (!string.Equals(address, _linkAddress, StringComparison.Ordinal))
        {
            EndLink();
            _linkAddress = address;
        }

        for (var index = 0; index < text.Marks.Count; index++)
        {
            if (text.Marks[index] is AdfUnknownMark unknown)
            {
                _text.Append($"{AdfMarkers.Unsupported(unknown.TypeName)} ", TextRole.Muted);
            }
        }

        var visible = WithSubSup(text);

        _text.Append(visible, RoleOf(text));

        if (_linkAddress is not null)
        {
            _linkText.Append(visible);
        }
    }

    private void EndLink()
    {
        if (_linkAddress is null)
        {
            return;
        }

        var shown = _linkText.ToString().Trim();

        if (!string.Equals(shown, _linkAddress, StringComparison.Ordinal))
        {
            _text.Append($" <{_linkAddress}>", TextRole.Link);
        }

        _linkAddress = null;
        _linkText.Clear();
    }

    private static string? LinkAddress(AdfText text)
    {
        for (var index = 0; index < text.Marks.Count; index++)
        {
            if (text.Marks[index] is AdfLinkMark { Href: { Length: > 0 } href })
            {
                return href;
            }
        }

        return null;
    }

    private static string WithSubSup(AdfText text)
    {
        for (var index = 0; index < text.Marks.Count; index++)
        {
            if (text.Marks[index] is AdfSubSupMark mark)
            {
                if (mark.Kind == SubscriptKind)
                {
                    return $"{SubscriptPrefix}{text.Text}";
                }

                if (mark.Kind == SuperscriptKind)
                {
                    return $"{SuperscriptPrefix}{text.Text}";
                }
            }
        }

        return text.Text;
    }

    private TextRole RoleOf(AdfText text)
    {
        var role = _baseRole;
        var inheritsMarks = _baseRole is TextRole.Body or TextRole.Quote or TextRole.Muted;
        var isLink = false;
        var isCode = false;

        for (var index = 0; index < text.Marks.Count; index++)
        {
            switch (text.Marks[index])
            {
                case AdfStrikeMark when inheritsMarks:
                    role = TextRole.Muted;
                    break;
                case AdfUnderlineMark or AdfEmphasisMark when inheritsMarks:
                    role = TextRole.Emphasis;
                    break;
                case AdfStrongMark when inheritsMarks:
                    role = TextRole.Strong;
                    break;
                case AdfLinkMark { Href: { Length: > 0 } }:
                    isLink = true;
                    break;
                case AdfCodeMark:
                    isCode = true;
                    break;
            }
        }

        if (isCode)
        {
            return TextRole.Code;
        }

        return isLink ? TextRole.Link : role;
    }

    private static string EmojiText(AdfEmoji emoji)
    {
        if (!string.IsNullOrEmpty(emoji.Text))
        {
            return emoji.Text;
        }

        if (string.IsNullOrEmpty(emoji.ShortName))
        {
            return string.Empty;
        }

        return $"{EmojiDelimiter}{emoji.ShortName.Trim(':')}{EmojiDelimiter}";
    }

    private static string DateText(AdfDate date)
    {
        if (date.TimestampMilliseconds is not { } milliseconds
            || milliseconds < _minTimestamp
            || milliseconds > _maxTimestamp)
        {
            return string.Empty;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    private static string MentionText(AdfMention mention)
    {
        if (string.IsNullOrWhiteSpace(mention.Text))
        {
            return UnknownMention;
        }

        if (mention.Text.StartsWith(MentionPrefix, StringComparison.Ordinal))
        {
            return mention.Text;
        }

        return $"{MentionPrefix}{mention.Text}";
    }

    private static TextRole StatusRole(AdfStatus status)
    {
        return status.Color switch
        {
            StatusColorGreen => TextRole.StatusDone,
            StatusColorBlue => TextRole.StatusInProgress,
            StatusColorRed => TextRole.Error,
            _ => TextRole.StatusToDo,
        };
    }
}
