namespace Geeratya.Core.Text;

/// <summary>One line of styled text: spans in order, no line break inside.</summary>
public sealed record StyledLine(IReadOnlyList<StyledSpan> Spans)
{
    /// <summary>The empty line.</summary>
    public static StyledLine Empty { get; } = new([]);

    /// <summary>The text of all spans with the styles dropped.</summary>
    public string PlainText => string.Concat(Spans.Select(span => span.Text));

    /// <summary>A line of one span.</summary>
    public static StyledLine Of(string text, TextRole role = TextRole.Body)
    {
        return new StyledLine([new StyledSpan(text, role)]);
    }
}
