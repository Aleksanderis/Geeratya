using Geeratya.Core.Sources;

namespace Geeratya.Tui.PrintFrame;

/// <summary>What <c>--print-frame --source</c> opens: a board by name, a JQL query or one issue.</summary>
public sealed record ScreenSource(ScreenSourceKind Kind, string Value)
{
    private const char Separator = ':';
    private const string BoardPrefix = "board";
    private const string JqlPrefix = "jql";
    private const string IssuePrefix = "issue";

    /// <summary>Parses <c>board:NAME</c>, <c>jql:QUERY</c> or <c>issue:KEY</c>; anything else (or an empty value) is null.</summary>
    public static ScreenSource? Parse(string text)
    {
        var split = text.IndexOf(Separator);

        if (split <= 0 || split == text.Length - 1)
        {
            return null;
        }

        var value = text[(split + 1)..].Trim();

        return text[..split] switch
        {
            BoardPrefix when value.Length > 0 => new ScreenSource(ScreenSourceKind.Board, value),
            JqlPrefix when value.Length > 0 => new ScreenSource(ScreenSourceKind.Jql, value),
            IssuePrefix when value.Length > 0 => new ScreenSource(ScreenSourceKind.Issue, value),
            _ => null,
        };
    }

    /// <summary>The command line the palette runs to open the source (an issue opens as a one-row search).</summary>
    public string PaletteText()
    {
        return Kind switch
        {
            ScreenSourceKind.Board => $"{PaletteCommands.Board} {Value}",
            ScreenSourceKind.Jql => $"{PaletteCommands.Jql} {Value}",
            _ => $"{PaletteCommands.Jql} key = {Value}",
        };
    }
}
