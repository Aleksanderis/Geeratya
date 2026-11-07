using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>One chip of the zoom strip as drawn: its section, its text and the first column it covers.</summary>
/// <param name="Id">The pane id (<see cref="PaneIds"/>).</param>
/// <param name="Text">The chip text, padded with one space on each side.</param>
/// <param name="Start">The first column of the chip, counted from the left edge of the overlay.</param>
public sealed record StripChip(string Id, string Text, int Start)
{
    /// <summary>The columns the chip covers.</summary>
    public int Width => DisplayWidth.Of(Text);
}

/// <summary>
/// The layout of the section strip of a zoomed pane (<c>docs/internal/ux-screens.md</c>, "Panes, focus and zoom"), shared by
/// the renderer, which draws it, and the mouse, which hit-tests it. A strip wider than the overlay drops sections from
/// the left until the current one is in view. Pure.
/// </summary>
public static class DetailStrip
{
    /// <summary>
    /// The chips that show in a strip <paramref name="width"/> columns wide, in order, with their columns.
    /// </summary>
    public static IReadOnlyList<StripChip> Chips(DetailDocument document, string? current, int width)
    {
        var texts = document.Strip.Select(section => $" {section.Label} ").ToList();
        var first = 0;

        while (first < texts.Count - 1 && ReachOf(document.Strip, texts, first, current) > width)
        {
            first++;
        }

        var chips = new List<StripChip>();
        var column = 0;

        for (var index = first; index < texts.Count; index++)
        {
            var chip = new StripChip(document.Strip[index].Id, texts[index], column);

            chips.Add(chip);
            column += chip.Width + 1;
        }

        return chips;
    }

    /// <summary>
    /// The section whose chip covers <paramref name="column"/>, null on a separator or past the strip.
    /// </summary>
    public static string? SectionAt(DetailDocument document, string? current, int width, int column)
    {
        var chip = Chips(document, current, width)
            .FirstOrDefault(candidate => column >= candidate.Start && column < candidate.Start + candidate.Width);

        return chip is not null && column < width ? chip.Id : null;
    }

    // The column where the current chip ends when the strip starts at chip first.
    private static int ReachOf(IReadOnlyList<StripSection> sections, List<string> texts, int first, string? current)
    {
        var reach = 0;

        for (var index = first; index < texts.Count; index++)
        {
            reach += DisplayWidth.Of(texts[index]);

            if (sections[index].Id == current)
            {
                return reach;
            }

            reach++;
        }

        return reach;
    }
}
