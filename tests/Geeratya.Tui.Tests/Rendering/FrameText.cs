using System.Text.RegularExpressions;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Reads a framed body as its content: the chips of the top edge joined by <c> · </c> as the first line, then the
/// lines between the side borders, without the bottom edge. Lets a test of a row or a title ignore the frame glyphs
/// (the frame itself is covered by the snapshots and the <c>FrameGridTests</c>).
/// </summary>
public static partial class FrameText
{
    private const string Separator = " · ";

    public static List<string> Unframe(IEnumerable<string> lines)
    {
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith('╭') || line.StartsWith('├'))
            {
                result.Add(Chips(line));
            }
            else if (line.StartsWith('╰'))
            {
                result.Add(string.Empty);
            }
            else if (line.StartsWith('│') && line.EndsWith('│'))
            {
                result.Add(line[1..^1]);
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static string Chips(string edge)
    {
        var parts = Rules().Split(edge[1..^1]).Where(part => part.Length > 0);

        return string.Join(Separator, parts);
    }

    [GeneratedRegex(@"\s*─+\s*")]
    private static partial Regex Rules();
}
