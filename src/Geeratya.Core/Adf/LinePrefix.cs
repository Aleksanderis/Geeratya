using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

/// <summary>Text put in front of a line (a list marker, a quote bar), with its display width.</summary>
internal readonly record struct LinePrefix(string Text, TextRole Role)
{
    public static LinePrefix None { get; } = new(string.Empty, TextRole.Body);

    public int Width => DisplayWidth.Of(Text);

    /// <summary>A prefix of blank columns as wide as this one, for the lines after a marker.</summary>
    public LinePrefix Hanging()
    {
        return new LinePrefix(new string(' ', Width), TextRole.Body);
    }
}
