using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The settings popup (<c>:settings</c>, <c>&lt;space&gt;,</c>): a floating <see cref="OverlayBox"/> over the dimmed view
/// of the tab, like the help popup. The categories are on the left, the rows of the selected one on the right with
/// their control at the right end (a toggle <c>[x]</c>, a choice <c>[value ▾]</c>, its open list under the row), then a
/// help row (what the setting does, its default and values) and a hint row. Every row comes from
/// <see cref="SettingsRows"/>, so a setting added to the registry shows up here. Under
/// <see cref="CategoriesMinimumWidth"/> columns the categories collapse into one list of rows with a heading per
/// category; in a terminal too small for that the box says so. The box shrinks to its content. Pure.
/// </summary>
public static class SettingsScreen
{
    /// <summary>The narrowest terminal that shows the category pane next to the rows.</summary>
    public const int CategoriesMinimumWidth = 80;

    /// <summary>The smallest terminal width that shows the rows at all.</summary>
    public const int MinimumWidth = 40;

    /// <summary>The smallest body height that shows the rows at all.</summary>
    public const int MinimumHeight = 9;

    private const string Title = "Settings";
    private const string TooSmall = "Terminal too small";
    private const string OpenMark = "▾";
    private const string ToggleOn = "[x]";
    private const string ToggleOff = "[ ]";
    private const int MaxBoxWidth = 96;
    private const int Margin = 4;
    private const int CategoryWidth = 18;
    private const int Gutter = 3;
    private const int LabelColumn = 18;
    private const int HelpRows = 2;
    private const int Chrome = 2 + 1 + HelpRows + 1 + 1;

    /// <summary>
    /// Draws the popup over <paramref name="body"/> (<paramref name="height"/> lines); the clickable rows are added to
    /// <paramref name="regions"/> (body-relative) when it is not null.
    /// </summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        BodyContext context,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        if (width < MinimumWidth || height < MinimumHeight)
        {
            return Place(body, SmallBox(width, height), new Placement(0, 0, width), regions);
        }

        var boxWidth = Math.Min(width >= 2 * MinimumWidth ? width - Margin : width, MaxBoxWidth);
        var rows = SettingsRows.Build(state);
        var categories = SettingsRows.Categories(rows);
        var selected = SettingsScreenReducer.Selected(state, rows.Count);
        var category = categories.ToList().IndexOf(rows[selected].Category);
        var wide = width >= CategoriesMinimumWidth && height - 2 - Chrome >= categories.Count;
        var inner = boxWidth - 4;
        var room = wide ? inner - CategoryWidth - Gutter : inner;
        var entries = Entries(state, rows, selected, wide ? categories[category] : null, room);
        var area = Math.Max(1, Math.Min(height - 2 - Chrome, Wanted(state, rows, categories, wide, room)));
        var top = Math.Max(0, (height - area - Chrome) / 2);
        var first = FirstVisible(entries, area);
        var box = Box(state, context, rows[selected], entries, new Layout(categories, category, wide, boxWidth, area, first));

        if (regions is not null)
        {
            var left = (width - boxWidth) / 2;

            OverlayBox.Cover(regions, top, left, boxWidth, box.Count);
            MarkRegions(regions, entries, first, area, new Placement(top, left, room), wide ? categories.Count : 0);
        }

        return Place(body, box, new Placement(top, (width - boxWidth) / 2, width), null);
    }

    private sealed record Layout(
        IReadOnlyList<string> Categories,
        int Category,
        bool Wide,
        int BoxWidth,
        int Area,
        int First);

    private sealed record Placement(int Top, int Left, int Width);

    // One display line of the rows pane: a heading, a row, or a word of the open choice list.
    private sealed record Entry(StyledLine Line, int Row, int Choice, bool Cursor = false);

    private static List<StyledLine> Box(
        AppState state,
        BodyContext context,
        SettingsRow row,
        List<Entry> entries,
        Layout layout)
    {
        var boxWidth = layout.BoxWidth;
        var inner = boxWidth - 4;
        var room = layout.Wide ? inner - CategoryWidth - Gutter : inner;
        var box = new List<StyledLine> { OverlayBox.Top(boxWidth, Title) };

        for (var line = 0; line < layout.Area; line++)
        {
            var spans = new List<StyledSpan>();

            if (layout.Wide)
            {
                spans.AddRange(CategoryCell(layout.Categories, layout.Category, line).Spans);
                spans.Add(new StyledSpan(" │ ", TextRole.Muted));
            }

            var entry = layout.First + line < entries.Count ? entries[layout.First + line].Line : StyledLine.Empty;

            spans.AddRange(StyledLines.Fit(entry, room).Spans);

            var marker = OverlayBox.ScrollMarker(line, layout.Area, layout.First, entries.Count);

            box.Add(OverlayBox.Wall(new StyledLine(spans), boxWidth - 2, marker));
        }

        box.Add(layout.Wide ? OverlayBox.Divider(boxWidth, 2 + CategoryWidth + 1) : OverlayBox.Divider(boxWidth));

        var help = ConfirmDialog.Wrap(HelpText(state, row), Math.Max(1, inner));

        for (var index = 0; index < HelpRows; index++)
        {
            var text = index < help.Count ? StyledLine.Of(help[index], TextRole.Muted) : StyledLine.Empty;

            box.Add(OverlayBox.Wall(text, boxWidth - 2));
        }

        box.Add(OverlayBox.Divider(boxWidth));
        box.Add(OverlayBox.Wall(OverlayBox.Hints(HintText(state, context, inner)), boxWidth - 2));
        box.Add(OverlayBox.Bottom(boxWidth));

        return box;
    }

    private static List<StyledLine> SmallBox(int width, int height)
    {
        if (width < 8 || height < 3)
        {
            return [];
        }

        return
        [
            OverlayBox.Top(width, Title),
            OverlayBox.Wall(StyledLine.Of(TooSmall, TextRole.Muted), width - 2),
            OverlayBox.Bottom(width),
        ];
    }

    private static List<StyledLine> Place(
        IReadOnlyList<StyledLine> body,
        List<StyledLine> box,
        Placement placement,
        List<OverlayRegion>? regions)
    {
        var result = OverlayBox.Backdrop(body);

        if (box.Count == 0)
        {
            return result;
        }

        OverlayBox.Cover(regions, placement.Top, placement.Left, TextWidth.Of(box[0].PlainText), box.Count);

        for (var index = 0; index < box.Count && placement.Top + index < result.Count; index++)
        {
            result[placement.Top + index] = OverlayBox.Over(
                result[placement.Top + index],
                box[index],
                placement.Left,
                placement.Width);
        }

        return result;
    }

    // The rows the box needs: the longest category (or the one list), plus the open choice list.
    private static int Wanted(
        AppState state,
        IReadOnlyList<SettingsRow> rows,
        IReadOnlyList<string> categories,
        bool wide,
        int room)
    {
        var choices = 0;

        if (state.SettingsScreen?.Choice is not null
            && rows[SettingsScreenReducer.Selected(state, rows.Count)].Setting is { } setting)
        {
            choices = SettingsRows.ChoicesOf(state, setting).Count;
        }

        if (!wide)
        {
            return Entries(state, rows, -1, null, room).Count + choices;
        }

        var longest = categories.Max(name => rows.Count(row => row.Category == name));

        return Math.Max(categories.Count, longest + choices);
    }

    private static StyledLine CategoryCell(IReadOnlyList<string> categories, int current, int line)
    {
        if (line >= categories.Count)
        {
            return StyledLine.Of(new string(' ', CategoryWidth));
        }

        var active = line == current;
        var label = $"{(active ? "> " : "  ")}{categories[line]}";
        var text = TextWidth.Pad(TextWidth.Fit(label, CategoryWidth), CategoryWidth);

        return StyledLine.Of(text, active ? TextRole.Heading : TextRole.Muted);
    }

    private static List<Entry> Entries(
        AppState state,
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
                entries.AddRange(ChoiceLines(state, setting, choice));
            }
        }

        return entries;
    }

    private static StyledLine RowLine(SettingsRow row, bool selected, int room)
    {
        var cursor = selected ? "> " : "  ";
        var control = Control(row);
        var labelRoom = Math.Max(1, Math.Min(LabelColumn, room - cursor.Length));
        var label = TextWidth.Pad(TextWidth.Fit(row.Label, labelRoom - 1), labelRoom);
        var rest = Math.Max(0, room - cursor.Length - labelRoom);
        var value = row.Setting is null ? TextWidth.Pad(TextWidth.Fit(control, rest), rest) : RightAligned(control, rest);

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{cursor}{label}{value}", room), TextRole.Selected);
        }

        var valueRole = row.Kind == SettingsRowKind.Setting ? TextRole.Body : TextRole.Muted;

        return new StyledLine(
        [
            new StyledSpan(cursor, TextRole.Muted),
            new StyledSpan(label, TextRole.Strong),
            new StyledSpan(value, valueRole),
        ]);
    }

    private static string RightAligned(string control, int width)
    {
        var fitted = TextWidth.Fit(control, width);

        return $"{new string(' ', Math.Max(0, width - TextWidth.Of(fitted)))}{fitted}";
    }

    // What sits at the right end of a row: a checkbox, the value of a choice in a bracketed chip, or plain text.
    private static string Control(SettingsRow row)
    {
        if (row.Setting is not { } setting)
        {
            return row.Value;
        }

        if (setting.Kind == SettingKind.Toggle)
        {
            return row.Value == SettingsRegistry.On ? ToggleOn : ToggleOff;
        }

        return $"[{row.Value} {OpenMark}]";
    }

    private static IEnumerable<Entry> ChoiceLines(AppState state, SettingDefinition setting, int choice)
    {
        var words = SettingsRows.ChoicesOf(state, setting);
        var current = setting.Read(state.Settings);

        for (var index = 0; index < words.Count; index++)
        {
            var cursor = index == choice ? ">" : " ";
            var saved = words[index] == current ? "●" : "○";
            var role = index == choice ? TextRole.Selected : TextRole.Body;

            yield return new Entry(StyledLine.Of($"    {cursor} {saved} {words[index]}", role), -1, index, index == choice);
        }
    }

    private static int FirstVisible(List<Entry> entries, int area)
    {
        var cursor = entries.FindLastIndex(static entry => entry.Cursor);

        return area <= 0 || cursor < area ? 0 : Math.Min(cursor - area + 1, Math.Max(0, entries.Count - area));
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

        return $"{note}Default: {setting.Default}. {setting.Help}{values}";
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

    private static string HintText(AppState state, BodyContext context, int inner)
    {
        var keymap = context.Keymap;
        var down = keymap.FindKeyLabel(InputMode.Normal, ActionIds.NavigateDown) ?? "j";
        var up = keymap.FindKeyLabel(InputMode.Normal, ActionIds.NavigateUp) ?? "k";
        var open = keymap.FindKeyLabel(InputMode.Normal, ActionIds.ItemOpen) ?? "Enter";
        var reset = keymap.FindKeyLabel(InputMode.Normal, ActionIds.SettingsReset);
        var cancel = keymap.FindKeyLabel(InputMode.Normal, ActionIds.Cancel) ?? "Esc";
        var listOpen = state.SettingsScreen?.Choice is not null;
        var chips = new List<string> { $"{down}/{up} move", $"{open} {(listOpen ? "keep" : "change")}" };

        if (reset is not null && !listOpen)
        {
            chips.Add($"{reset} reset");
        }

        chips.Add($"{cancel} {(listOpen ? "cancel" : "close")}");

        // Narrow boxes keep the close chip: drop reset, then move, when the row does not fit.
        var text = string.Join("  ", chips);

        while (TextWidth.Of(text) > inner && chips.Count > 2)
        {
            chips.RemoveAt(chips.Count > 3 ? chips.Count - 2 : 0);
            text = string.Join("  ", chips);
        }

        return text;
    }

    private static void MarkRegions(
        List<OverlayRegion> regions,
        List<Entry> entries,
        int first,
        int area,
        Placement placement,
        int categoryCount)
    {
        var rowsLeft = placement.Left + 2 + (categoryCount > 0 ? CategoryWidth + Gutter : 0);
        var room = placement.Width;

        for (var index = 0; index < Math.Min(categoryCount, area); index++)
        {
            var region = new OverlayRegion(
                MouseTarget.SettingsCategory,
                placement.Top + 1 + index,
                placement.Left + 2,
                CategoryWidth,
                1,
                index);

            regions.Add(region);
        }

        var shown = entries.Skip(first).Take(area).ToList();

        for (var line = 0; line < shown.Count; line++)
        {
            var entry = shown[line];
            var row = placement.Top + 1 + line;

            if (entry.Row >= 0)
            {
                regions.Add(new OverlayRegion(MouseTarget.SettingsRow, row, rowsLeft, room, 1, entry.Row));
            }
            else if (entry.Choice >= 0)
            {
                regions.Add(new OverlayRegion(MouseTarget.SettingsChoice, row, rowsLeft, room, 1, entry.Choice));
            }
        }
    }
}
