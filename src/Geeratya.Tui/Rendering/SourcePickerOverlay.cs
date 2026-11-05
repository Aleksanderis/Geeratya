using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The source picker box (<c>docs/ux-screens.md</c>, "Source picker overlay"): a centred box over the body with the
/// filter prompt, the rows in groups (Boards, Filters, Query) and a key footer. Pure. The box rows replace the whole
/// body row they sit on, so nothing of the screen behind shows through at their sides.
/// </summary>
public static class SourcePickerOverlay
{
    private const int PromptMargin = 4;
    private const string Title = "Open a view";
    private const string SprintTitle = "Switch sprint";
    private const string TabsTitle = "Open tabs";
    private const string MoveTitle = "Move to sprint";
    private const string ActiveHeading = "Active";
    private const string FutureHeading = "Future";
    private const string ClosedHeading = "Closed";
    private const string LoadingText = "Loading…";
    private const string NothingText = "Nothing to choose from.";
    private const int MaxBoxWidth = 76;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int FooterHeight = 2;
    private const int FooterMinimumInner = 8;
    private const int KindColumn = 8;
    private const int TopGap = 2;
    private const int MaxBoxHeight = 18;

    /// <summary>Draws the picker of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var picker = state.Picker;
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (picker is null || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var rows = Rows(picker, state.Session.Sources, boxWidth - 4);
        var inner = Math.Min(Math.Min(height, MaxBoxHeight), rows.Lines.Count + 1 + FooterHeight + 2) - 2;
        var footer = inner >= FooterMinimumInner ? FooterHeight : 0;
        var box = BoxLines(picker, rows, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, box.Count);

        var area = inner - 1 - footer;
        var visible = rows.ChoiceOfLine.Skip(FirstVisible(rows, area)).Take(Math.Max(0, area));

        OverlayBox.Mark(regions, MouseTarget.PickerRow, top, left, boxWidth, 2, visible);

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], box[index], left, width);
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
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, TitleOf(picker)), OverlayBox.Wall(Prompt(picker, contentWidth), contentWidth) };

        var shown = rows.Lines.Skip(first).Take(Math.Max(0, area)).ToList();

        for (var row = 0; row < shown.Count; row++)
        {
            var marker = OverlayBox.ScrollMarker(row, area, first, rows.Lines.Count);

            lines.Add(OverlayBox.Wall(shown[row], contentWidth, marker));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(OverlayBox.Hints(FooterText(keymap, picker)), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    private static string TitleOf(SourcePickerState picker)
    {
        if (picker.Scope == PickerScope.Tabs)
        {
            return TabsTitle;
        }

        if (picker.Scope != PickerScope.Sprints)
        {
            return Title;
        }

        return picker.Sprint?.MoveKeys is { } keys ? $"{MoveTitle}: {MovedText(keys)}" : SprintTitle;
    }

    private static string MovedText(IReadOnlyList<string> keys)
    {
        return keys.Count == 1 ? keys[0] : $"{keys.Count.ToString(CultureInfo.InvariantCulture)} issues";
    }

    private static int FirstVisible(PickerRows rows, int area)
    {
        if (area <= 0 || rows.SelectedLine < area)
        {
            return 0;
        }

        return Math.Min(rows.SelectedLine - area + 1, Math.Max(0, rows.Lines.Count - area));
    }

    private static StyledLine Prompt(SourcePickerState picker, int contentWidth)
    {
        var text = PromptSpans.Of(new PromptText(picker.Text, picker.Back), contentWidth - PromptMargin, TextRole.Body);

        return new StyledLine([new StyledSpan("> ", TextRole.Key), .. text]);
    }

    private static string FooterText(KeymapTable keymap, SourcePickerState picker)
    {
        var up = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerPrevious);
        var down = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerNext);
        var accept = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);
        var cancel = keymap.FindKeyLabel(InputMode.Insert, ActionIds.Cancel);

        if (picker.Scope == PickerScope.Tabs)
        {
            return $"{up}/{down} choose   {accept} switch   {cancel} close";
        }

        if (picker.Scope == PickerScope.Sprints)
        {
            if (picker.Sprint?.MoveKeys is not null)
            {
                return $"{up}/{down} choose   {accept} move   {cancel} close";
            }

            var newTab = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerOpenNewTab);

            return $"{up}/{down} choose   {accept} open   {newTab} new tab   {cancel} close";
        }

        return $"{up}/{down} choose   {accept} open in a new tab   {cancel} close";
    }

    private static PickerRows Rows(SourcePickerState picker, SourceLists lists, int width)
    {
        var choices = SourceChoices.For(picker, lists);
        var selected = Math.Clamp(picker.Selected, 0, Math.Max(0, choices.Count - 1));
        var lines = new List<StyledLine>();
        var choiceOfLine = new List<int>();
        var selectedLine = 0;

        if (picker.Scope == PickerScope.Tabs)
        {
            return TabRows(choices, selected, width);
        }

        if (picker.Scope == PickerScope.Sprints)
        {
            return SprintRows(picker, choices, selected, width);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Views)
        {
            AddGroup(
                lines,
                "Saved views",
                [SourceChoiceKind.SavedView],
                choices,
                selected,
                width,
                ref selectedLine,
                lists,
                choiceOfLine);
        }

        if (picker.Scope is not (PickerScope.Filters or PickerScope.Jql or PickerScope.Views))
        {
            AddGroup(
                lines,
                "Boards",
                BoardGroup(picker.Scope),
                choices,
                selected,
                width,
                ref selectedLine,
                lists,
                choiceOfLine);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Filters)
        {
            AddGroup(
                lines,
                "Filters",
                [SourceChoiceKind.Filter],
                choices,
                selected,
                width,
                ref selectedLine,
                lists,
                choiceOfLine);
        }

        AddGroup(
                lines,
                "Recent",
                [SourceChoiceKind.Recent],
                choices,
                selected,
                width,
                ref selectedLine,
                lists,
                choiceOfLine);
        AddGroup(
                lines,
                "Query",
                [SourceChoiceKind.Query],
                choices,
                selected,
                width,
                ref selectedLine,
                lists,
                choiceOfLine);

        if (lines.Count == 0)
        {
            lines.Add(StyledLine.Of(NothingText, TextRole.Muted));
            choiceOfLine.Add(-1);
        }

        return new PickerRows(lines, selectedLine, choiceOfLine);
    }

    private static PickerRows TabRows(IReadOnlyList<SourceChoice> choices, int selected, int width)
    {
        if (choices.Count == 0)
        {
            return new PickerRows([StyledLine.Of(NothingText, TextRole.Muted)], 0, [-1]);
        }

        var lines = new List<StyledLine>(choices.Count);
        var choiceOfLine = new List<int>(choices.Count);

        for (var index = 0; index < choices.Count; index++)
        {
            lines.Add(ChoiceLine(choices[index], index == selected, width));
            choiceOfLine.Add(index);
        }

        return new PickerRows(lines, selected, choiceOfLine);
    }

    private static PickerRows SprintRows(
        SourcePickerState picker,
        IReadOnlyList<SourceChoice> choices,
        int selected,
        int width)
    {
        var lines = new List<StyledLine>();
        var choiceOfLine = new List<int>();
        var selectedLine = 0;
        var data = picker.Sprint;

        if (data?.Sprints is null)
        {
            return new PickerRows([SprintStateLine(data)], 0, [-1]);
        }

        foreach (var heading in new[] { ActiveHeading, FutureHeading, ClosedHeading })
        {
            var members = Enumerable
                .Range(0, choices.Count)
                .Where(index => SprintHeading(choices[index]) == heading)
                .ToList();

            if (members.Count == 0)
            {
                continue;
            }

            lines.Add(StyledLine.Of(heading, TextRole.Heading));
            choiceOfLine.Add(-1);

            foreach (var index in members)
            {
                if (index == selected)
                {
                    selectedLine = lines.Count;
                }

                lines.Add(ChoiceLine(choices[index], index == selected, width));
                choiceOfLine.Add(index);
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(StyledLine.Of(NothingText, TextRole.Muted));
            choiceOfLine.Add(-1);
        }

        return new PickerRows(lines, selectedLine, choiceOfLine);
    }

    private static StyledLine SprintStateLine(SprintPickerData? data)
    {
        return data?.Error is { } error
            ? StyledLine.Of($"  ! {error}", TextRole.Error)
            : StyledLine.Of($"  {LoadingText}", TextRole.Loading);
    }

    private static string SprintHeading(SourceChoice choice)
    {
        return choice.Sprint?.State switch
        {
            SprintState.Active => ActiveHeading,
            SprintState.Future => FutureHeading,
            _ => ClosedHeading,
        };
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
        SourceLists lists,
        List<int> choiceOfLine)
    {
        var members = new List<StyledLine>();
        var memberChoices = new List<int>();

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
                memberChoices.Add(index);
            }
        }

        var notices = StateLines(heading, lists, members.Count).ToList();

        members.AddRange(notices);
        memberChoices.AddRange(notices.Select(static _ => -1));

        if (members.Count > 0)
        {
            lines.Add(StyledLine.Of(heading, TextRole.Heading));
            choiceOfLine.Add(-1);
            lines.AddRange(members);
            choiceOfLine.AddRange(memberChoices);
        }
    }

    /// <summary>The loading or error row of a group whose list is not available (and only then).</summary>
    private static IEnumerable<StyledLine> StateLines(string heading, SourceLists lists, int memberCount)
    {
        var kind = heading == "Filters" ? SourceListKind.Filters : SourceListKind.Boards;

        if (heading is "Query" or "Recent" or "Saved views" || memberCount > 0 || lists.IsLoaded(kind))
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
        var marker = " ";
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
            SourceChoiceKind.Recent => "Recent",
            SourceChoiceKind.SavedView => "View",
            SourceChoiceKind.Tab => "Tab",
            SourceChoiceKind.Sprint or SourceChoiceKind.MoreClosed => string.Empty,
            _ => "JQL",
        };
    }

    private sealed record PickerRows(List<StyledLine> Lines, int SelectedLine, List<int> ChoiceOfLine);
}
