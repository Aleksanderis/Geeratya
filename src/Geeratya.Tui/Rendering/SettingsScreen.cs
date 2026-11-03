using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The settings screen (<c>:settings</c>, <c>&lt;space&gt;,</c>): a full-body frame with the categories on the left and
/// the rows of the selected category on the right, a help row (what the highlighted setting does, its default and
/// values) and a hint row. Every row comes from <see cref="SettingsRows"/>, so a setting added to the registry shows
/// up here. Under <see cref="CategoriesMinimumWidth"/> columns the categories collapse into the one list of rows with
/// a heading per category. Pure.
/// </summary>
public static class SettingsScreen
{
    /// <summary>The narrowest body that shows the category pane next to the rows.</summary>
    public const int CategoriesMinimumWidth = 60;

    private const string Title = "Settings";
    private const string CategoriesTitle = "Categories";
    private const int CategoryWidth = 20;
    private const int LabelColumn = 18;
    private const int HelpRows = 2;
    private const int HintRows = 1;
    private const int HelpMinimumInner = 8;
    private const int HintMinimumInner = 5;
    private const string SavedMark = "●";
    private const string SavedMarkAscii = "*";

    /// <summary>
    /// The body lines for <paramref name="state"/> (exactly <paramref name="height"/> lines); the clickable rows are
    /// added to <paramref name="regions"/> (body-relative) when it is not null.
    /// </summary>
    public static List<StyledLine> Render(
        AppState state,
        BodyContext context,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var rows = SettingsRows.Build(state);
        var categories = SettingsRows.Categories(rows);
        var selected = SettingsScreenReducer.Selected(state, rows.Count);
        var category = categories.ToList().IndexOf(rows[selected].Category);
        var wide = width >= CategoriesMinimumWidth;
        var framed = FrameGrid.CanFrame(width, height);
        var inner = framed ? Math.Max(0, height - 2) : height;
        var footer = FooterRows(inner);
        var area = Math.Max(0, inner - footer);
        var catWidth = wide ? CategoryWidth : 0;
        var room = Math.Max(1, width - catWidth - (framed ? 2 : 0) - 1);
        var entries = Entries(state, context, rows, selected, wide ? categories[category] : null, room);
        var first = FirstVisible(entries, area);
        var right = new List<StyledLine>(inner);

        right.AddRange(entries.Skip(first).Take(area).Select(static entry => entry.Line));

        while (right.Count < area)
        {
            right.Add(StyledLine.Empty);
        }

        right.AddRange(Footer(state, context, rows[selected], room, footer));

        var left = wide ? CategoryLines(categories, category, inner) : [];

        if (regions is not null)
        {
            MarkRegions(regions, entries, first, area, framed, width, height, catWidth, categories);
        }

        if (!framed)
        {
            return [.. right.Take(height), .. Enumerable.Repeat(StyledLine.Empty, Math.Max(0, height - right.Count))];
        }

        return Frame(width, height, catWidth, categories[category], left, right);
    }

    private static int FooterRows(int inner)
    {
        if (inner >= HelpMinimumInner)
        {
            return HelpRows + HintRows;
        }

        return inner >= HintMinimumInner ? HintRows : 0;
    }

    private static List<StyledLine> Frame(
        int width,
        int height,
        int catWidth,
        string category,
        List<StyledLine> left,
        List<StyledLine> right)
    {
        var wide = catWidth > 0;
        var rowsPane = wide
            ? new PaneRect(0, catWidth - 1, height, width - catWidth + 1)
            : new PaneRect(0, 0, height, width);
        var panes = new List<PaneRect>();

        if (wide)
        {
            panes.Add(new PaneRect(0, 0, height, catWidth));
        }

        panes.Add(rowsPane);

        var grid = new FrameGrid(width, height, panes);
        var chips = new List<EdgeChip>();

        if (wide)
        {
            chips.AddRange(FrameGrid.Chips(panes[0], [new StyledSpan(CategoriesTitle, TextRole.Muted)]));
            chips.AddRange(FrameGrid.Chips(rowsPane, [new StyledSpan($"{Title}: {category}", TextRole.Heading)]));
        }
        else
        {
            chips.AddRange(FrameGrid.Chips(rowsPane, [new StyledSpan(Title, TextRole.Heading)]));
        }

        var lines = new List<StyledLine>(height) { new(grid.Row(0, static (_, _) => [], chips)) };

        for (var row = 1; row < height - 1; row++)
        {
            var index = row - 1;
            var spans = grid.Row(
                row,
                (start, length) => wide && start < catWidth - 1
                    ? Padded(left, index, length)
                    : Padded(right, index, length));

            lines.Add(new StyledLine(spans));
        }

        lines.Add(new StyledLine(grid.Row(height - 1, static (_, _) => [])));

        return lines;
    }

    private static IReadOnlyList<StyledSpan> Padded(List<StyledLine> lines, int index, int length)
    {
        var line = index < lines.Count ? lines[index] : StyledLine.Empty;

        if (length < 2)
        {
            return StyledLines.Fit(line, length).Spans;
        }

        return [new StyledSpan(" ", TextRole.Body), .. StyledLines.Fit(line, length - 1).Spans];
    }

    private static List<StyledLine> CategoryLines(IReadOnlyList<string> categories, int current, int inner)
    {
        var lines = new List<StyledLine>(inner);

        for (var index = 0; index < categories.Count && lines.Count < inner; index++)
        {
            var name = TextWidth.Pad(TextWidth.Fit(categories[index], CategoryWidth - 5), CategoryWidth - 4);

            lines.Add(index == current
                ? StyledLine.Of($"> {name}", TextRole.Selected)
                : new StyledLine([new StyledSpan("  ", TextRole.Body), new StyledSpan(name, TextRole.Strong)]));
        }

        return lines;
    }

    // One display line of the rows pane: a heading, a row, or a word of the open choice list.
    private sealed record Entry(StyledLine Line, int Row, int Choice, bool Cursor = false);

    private static List<Entry> Entries(
        AppState state,
        BodyContext context,
        IReadOnlyList<SettingsRow> rows,
        int selected,
        string? onlyCategory,
        int room)
    {
        var entries = new List<Entry>();
        var heading = (string?)null;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (onlyCategory is not null && row.Category != onlyCategory)
            {
                continue;
            }

            if (onlyCategory is null && row.Category != heading)
            {
                heading = row.Category;
                entries.Add(new Entry(StyledLine.Of(heading, TextRole.Heading), -1, -1));
            }

            entries.Add(new Entry(RowLine(row, index == selected, room), index, -1, index == selected));

            if (index == selected && state.SettingsScreen?.Choice is { } choice && row.Setting is { } setting)
            {
                entries.AddRange(ChoiceLines(state, context, setting, choice));
            }
        }

        return entries;
    }

    private static StyledLine RowLine(SettingsRow row, bool selected, int room)
    {
        var label = TextWidth.Pad(TextWidth.Fit(row.Label, LabelColumn - 1), LabelColumn);
        var cursor = selected ? "> " : "  ";

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{cursor}{label}{row.Value}", room), TextRole.Selected);
        }

        var valueRole = row.Kind == SettingsRowKind.Setting ? TextRole.Body : TextRole.Muted;

        return new StyledLine(
        [
            new StyledSpan(cursor, TextRole.Muted),
            new StyledSpan(label, TextRole.Strong),
            new StyledSpan(row.Value, valueRole),
        ]);
    }

    private static IEnumerable<Entry> ChoiceLines(
        AppState state,
        BodyContext context,
        SettingDefinition setting,
        int choice)
    {
        var words = SettingsRows.ChoicesOf(state, setting);
        var current = setting.Read(state.Settings);
        var mark = context.Glyphs == GlyphSet.Ascii ? SavedMarkAscii : SavedMark;

        for (var index = 0; index < words.Count; index++)
        {
            var cursor = index == choice ? ">" : " ";
            var saved = words[index] == current ? mark : " ";
            var text = $"    {cursor} {saved} {words[index]}";

            yield return new Entry(
                StyledLine.Of(text, index == choice ? TextRole.Selected : TextRole.Body),
                -1,
                index,
                index == choice);
        }
    }

    private static int FirstVisible(List<Entry> entries, int area)
    {
        var cursor = entries.FindLastIndex(static entry => entry.Cursor);

        return area <= 0 || cursor < area ? 0 : Math.Min(cursor - area + 1, Math.Max(0, entries.Count - area));
    }

    private static List<StyledLine> Footer(
        AppState state,
        BodyContext context,
        SettingsRow row,
        int room,
        int footer)
    {
        var lines = new List<StyledLine>(footer);

        if (footer >= HelpRows + HintRows)
        {
            var wrapped = ConfirmDialog.Wrap(HelpText(state, row), Math.Max(1, room));

            for (var index = 0; index < HelpRows; index++)
            {
                lines.Add(index < wrapped.Count ? StyledLine.Of(wrapped[index], TextRole.Muted) : StyledLine.Empty);
            }
        }

        if (footer >= HintRows)
        {
            lines.Add(new StyledLine(HintBar.Spans(Hints(state, context))));
        }

        return lines;
    }

    private static string HelpText(AppState state, SettingsRow row)
    {
        if (state.SettingsScreen?.Preview is { } preview)
        {
            var previewing = $"Previewing {preview}: Enter keeps it, Esc restores {state.Settings.Theme}.";

            return $"{TierNote(state, row)}{previewing}";
        }

        if (row.Setting is not { } setting)
        {
            return row.Help;
        }

        var words = SettingsRows.ChoicesOf(state, setting);
        var note = TierNote(state, row);

        // With the note the help row has no room for the values; the open list shows them anyway.
        var values = words.Count is > 0 and <= 6 && note.Length == 0
            ? $" Values: {string.Join(", ", words)}."
            : string.Empty;

        return $"{note}{setting.Help} Default: {setting.Default}.{values}";
    }

    // The theme row leads with why a theme may look different here (first, so a narrow help row keeps it); other
    // rows and full colour have no note.
    private static string TierNote(AppState state, SettingsRow row)
    {
        if (row.Setting?.Name != SettingsRegistry.ThemeName)
        {
            return string.Empty;
        }

        return state.SettingsInfo.Tier switch
        {
            ColourTier.Basic16 => "16 colours here: themes are approximated. ",
            ColourTier.None => "No colour here: themes change nothing. ",
            _ => string.Empty,
        };
    }

    private static List<HintChip> Hints(AppState state, BodyContext context)
    {
        var keymap = context.Keymap;
        var down = keymap.FindKeyLabel(InputMode.Normal, ActionIds.NavigateDown) ?? "j";
        var up = keymap.FindKeyLabel(InputMode.Normal, ActionIds.NavigateUp) ?? "k";
        var open = keymap.FindKeyLabel(InputMode.Normal, ActionIds.ItemOpen) ?? "Enter";
        var reset = keymap.FindKeyLabel(InputMode.Normal, ActionIds.SettingsReset);
        var cancel = keymap.FindKeyLabel(InputMode.Normal, ActionIds.Cancel) ?? "Esc";
        var listOpen = state.SettingsScreen?.Choice is not null;
        var chips = new List<HintChip> { new($"{down}/{up}", "move"), new(open, listOpen ? "keep" : "change") };

        if (reset is not null && !listOpen)
        {
            chips.Add(new HintChip(reset, "reset"));
        }

        chips.Add(new HintChip(cancel, listOpen ? "cancel" : "close"));

        return chips;
    }

    private static void MarkRegions(
        List<OverlayRegion> regions,
        List<Entry> entries,
        int first,
        int area,
        bool framed,
        int width,
        int height,
        int catWidth,
        IReadOnlyList<string> categories)
    {
        var offset = framed ? 1 : 0;
        var left = framed ? 1 : 0;

        // The whole body is the screen: a click on a part of it that is no row does nothing.
        OverlayBox.Cover(regions, 0, 0, width, height);

        if (catWidth > 0)
        {
            for (var index = 0; index < categories.Count; index++)
            {
                regions.Add(
                    new OverlayRegion(MouseTarget.SettingsCategory, offset + index, left, catWidth - 2, 1, index));
            }
        }

        var rowsLeft = catWidth > 0 ? catWidth : left;
        var rowsWidth = Math.Max(1, width - rowsLeft - (framed ? 1 : 0));
        var shown = entries.Skip(first).Take(area).ToList();

        for (var line = 0; line < shown.Count; line++)
        {
            var entry = shown[line];

            if (entry.Row >= 0)
            {
                regions.Add(
                    new OverlayRegion(MouseTarget.SettingsRow, offset + line, rowsLeft, rowsWidth, 1, entry.Row));
            }
            else if (entry.Choice >= 0)
            {
                regions.Add(
                    new OverlayRegion(MouseTarget.SettingsChoice, offset + line, rowsLeft, rowsWidth, 1, entry.Choice));
            }
        }
    }
}
