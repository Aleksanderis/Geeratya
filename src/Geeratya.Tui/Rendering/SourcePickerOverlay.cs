using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The source picker box (<c>docs/ux-screens.md</c>, "Source picker overlay"): a centred box over the body with the
/// filter prompt, the rows in groups (Boards, Filters, Query) and a key footer. Pure. The box rows replace the whole
/// body row they sit on, so nothing of the screen behind shows through at their sides.
/// </summary>
public static class SourcePickerOverlay
{
    private const string Title = "Open a view";
    private const string LoadingText = "Loading…";
    private const string NothingText = "Nothing to choose from.";
    private const int MaxBoxWidth = 100;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int FooterHeight = 2;
    private const int FooterMinimumInner = 8;
    private const int KindColumn = 8;
    private const int TopGap = 2;

    /// <summary>Draws the picker of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var picker = state.Picker;
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (picker is null || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var rows = Rows(picker, state.Session.Sources, boxWidth - 4);
        var inner = Math.Min(height, rows.Lines.Count + 1 + FooterHeight + 2) - 2;
        var footer = inner >= FooterMinimumInner ? FooterHeight : 0;
        var box = BoxLines(picker, rows, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = body.ToList();

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Place(box[index], left, width);
        }

        return result;
    }

    private static List<StyledLine> BoxLines(
        SourcePickerState picker,
        PickerRows rows,
        KeymapTable keymap,
        int boxWidth,
        int inner,
        int footer)
    {
        var contentWidth = boxWidth - 2;
        var area = inner - 1 - footer;
        var first = FirstVisible(rows, area);
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, Title), OverlayBox.Wall(Prompt(picker), contentWidth) };

        foreach (var line in rows.Lines.Skip(first).Take(Math.Max(0, area)))
        {
            lines.Add(OverlayBox.Wall(line, contentWidth));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(keymap), TextRole.Muted), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    private static int FirstVisible(PickerRows rows, int area)
    {
        if (area <= 0 || rows.SelectedLine < area)
        {
            return 0;
        }

        return Math.Min(rows.SelectedLine - area + 1, Math.Max(0, rows.Lines.Count - area));
    }

    private static StyledLine Prompt(SourcePickerState picker)
    {
        return new StyledLine([new StyledSpan("> ", TextRole.Key), new StyledSpan($"{picker.Text}_", TextRole.Body)]);
    }

    private static string FooterText(KeymapTable keymap)
    {
        var up = keymap.FindKeys(InputMode.Insert, ActionIds.PickerPrevious);
        var down = keymap.FindKeys(InputMode.Insert, ActionIds.PickerNext);
        var accept = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        return $"{up}/{down} choose   {accept} open in a new tab   {cancel} close";
    }

    private static PickerRows Rows(SourcePickerState picker, SourceLists lists, int width)
    {
        var choices = SourceChoices.For(picker, lists);
        var selected = Math.Clamp(picker.Selected, 0, Math.Max(0, choices.Count - 1));
        var lines = new List<StyledLine>();
        var selectedLine = 0;

        if (picker.Scope != PickerScope.Filters)
        {
            AddGroup(lines, "Boards", BoardGroup(picker.Scope), choices, selected, width, ref selectedLine, lists);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Filters)
        {
            AddGroup(lines, "Filters", [SourceChoiceKind.Filter], choices, selected, width, ref selectedLine, lists);
        }

        AddGroup(lines, "Query", [SourceChoiceKind.Query], choices, selected, width, ref selectedLine, lists);

        if (lines.Count == 0)
        {
            lines.Add(StyledLine.Of(NothingText, TextRole.Muted));
        }

        return new PickerRows(lines, selectedLine);
    }

    private static SourceChoiceKind[] BoardGroup(PickerScope scope)
    {
        return scope == PickerScope.Backlogs ? [SourceChoiceKind.Backlog] : [SourceChoiceKind.Board, SourceChoiceKind.Backlog];
    }

    private static void AddGroup(
        List<StyledLine> lines,
        string heading,
        SourceChoiceKind[] kinds,
        IReadOnlyList<SourceChoice> choices,
        int selected,
        int width,
        ref int selectedLine,
        SourceLists lists)
    {
        var members = new List<StyledLine>();

        for (var index = 0; index < choices.Count; index++)
        {
            if (kinds.Contains(choices[index].Kind))
            {
                var isSelected = index == selected;

                if (isSelected)
                {
                    selectedLine = lines.Count + 1 + members.Count;
                }

                members.Add(ChoiceLine(choices[index], isSelected, width));
            }
        }

        members.AddRange(StateLines(heading, lists, members.Count));

        if (members.Count > 0)
        {
            lines.Add(StyledLine.Of(heading, TextRole.Heading));
            lines.AddRange(members);
        }
    }

    /// <summary>The loading or error row of a group whose list is not available (and only then).</summary>
    private static IEnumerable<StyledLine> StateLines(string heading, SourceLists lists, int memberCount)
    {
        var kind = heading == "Filters" ? SourceListKind.Filters : SourceListKind.Boards;

        if (heading == "Query" || memberCount > 0 || lists.IsLoaded(kind))
        {
            return [];
        }

        if (lists.ErrorOf(kind) is { } error)
        {
            return [StyledLine.Of($"  ! {error}", TextRole.Error)];
        }

        return [StyledLine.Of($"  {LoadingText}", TextRole.Loading)];
    }

    private static StyledLine ChoiceLine(SourceChoice choice, bool selected, int width)
    {
        var kind = KindText(choice.Kind);
        var detail = choice.Detail.Length == 0 ? string.Empty : $" {choice.Detail}";
        var marker = selected ? ">" : " ";
        var text = $"{marker} {kind.PadRight(KindColumn)}{choice.Name}{detail}";

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad(text, width), TextRole.Selected);
        }

        var prefix = $"{marker} {kind.PadRight(KindColumn)}";

        return new StyledLine(
        [
            new StyledSpan(prefix, TextRole.Muted),
            new StyledSpan(choice.Name, TextRole.Body),
            new StyledSpan(detail, TextRole.Muted),
        ]);
    }

    private static string KindText(SourceChoiceKind kind)
    {
        return kind switch
        {
            SourceChoiceKind.Board => "Board",
            SourceChoiceKind.Backlog => "Backlog",
            SourceChoiceKind.Filter => "Filter",
            _ => "JQL",
        };
    }

    private sealed record PickerRows(List<StyledLine> Lines, int SelectedLine);
}
