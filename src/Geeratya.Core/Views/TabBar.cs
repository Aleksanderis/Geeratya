using Geeratya.Core.Keymap;
using Geeratya.Core.Text;

namespace Geeratya.Core.Views;

/// <summary>
/// The tab bar line (<c>docs/ux-look.md</c>, section 5). Pure layout: one padded chip per tab (<c> 1 Board GCM </c>,
/// the active one filled, drawn on the bar background), titles cut to 24 cells and shrunk towards 8 when the line is too
/// short; when even that does not fit a window of tabs around the active one is shown with <c>‹n</c> and <c>+n ›</c>
/// chips for the hidden ends, and under <see cref="CompactBelow"/> columns only the active chip and <c>+</c> remain.
/// </summary>
public static class TabBar
{
    /// <summary>Rows the tab bar takes at the top of the screen.</summary>
    public const int Rows = 1;

    /// <summary>The longest title of an inactive tab, in cells.</summary>
    public const int InactiveTitleWidth = 24;

    /// <summary>The longest title of the active tab, in cells.</summary>
    public const int ActiveTitleWidth = 24;

    /// <summary>The shortest title the bar shrinks to before it hides tabs.</summary>
    public const int MinimumTitleWidth = 8;

    /// <summary>Under this many columns only the active chip and the <c>+</c> remain.</summary>
    public const int CompactBelow = 40;

    /// <summary>From this width the <c> : commands </c> chip is drawn at the right end, and the tabs make room for it.</summary>
    public const int CommandsFrom = 44;

    /// <summary>From this width the active chip carries its close <c>x</c>.</summary>
    public const int CloseFrom = 80;

    private const string DefaultCommandsKey = ":";
    private const string NewTabText = " + ";
    private const string DefaultJumpLabel = "<space>g";
    private const string Separator = " ";
    private const string LoadingMark = " …";
    private const string FailedMark = " !";
    private const string CloseMark = " x";

    /// <summary>
    /// The bar for <paramref name="tabs"/>; empty when there are none. It is at most <paramref name="width"/> cells
    /// wide unless the terminal is too narrow for even one tab; the frame cuts it then.
    /// </summary>
    public static StyledLine Build(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        return Layout(tabs, active, width, keymap).Line;
    }

    /// <summary>
    /// The cells each drawn tab occupies in the bar built for <paramref name="width"/>, in the order drawn. A mouse
    /// click on a cell inside an area activates that tab (the hidden-tab marks <c>‹n</c> and <c>+n ›</c> are areas
    /// too: they step to the neighbouring hidden tab); the separators are not.
    /// </summary>
    public static IReadOnlyList<TabArea> Areas(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        return Layout(tabs, active, width, keymap).TabAreas;
    }

    /// <summary>
    /// The cells of the <c>+</c> button after the last tab, null when the bar has no room for it or hides tabs.
    /// </summary>
    public static TabButton? NewTabButton(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        return Layout(tabs, active, width, keymap).NewTab;
    }

    /// <summary>
    /// The cells of the <c>x</c> on the active chip (the x and the pad after it), null when the bar draws none (under
    /// <see cref="CloseFrom"/> columns, or the active tab is not shown).
    /// </summary>
    public static TabButton? CloseButton(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        foreach (var area in Layout(tabs, active, width, keymap).TabAreas)
        {
            if (area.Close is { } close && area.Tab == active)
            {
                return close;
            }
        }

        return null;
    }

    /// <summary>
    /// The cells of the jump chip at the right end of the bar, null when there are no tabs or the bar leaves
    /// no room (a spare cell between the last tab item or <c>+</c> and the chip). A click on it opens the
    /// jump prompt. <paramref name="keymap"/> names the keys on the chip (the default binding without it).
    /// </summary>
    public static TabButton? JumpChip(IReadOnlyList<TabState> tabs, TabId? active, int width, KeymapTable? keymap = null)
    {
        return RightChips(tabs, active, width, keymap).Jump;
    }

    /// <summary>
    /// The cells of the <c> : commands </c> chip at the right end of the bar (left of the jump chip), null under
    /// <see cref="CommandsFrom"/> columns. The tabs leave room for it, so it shows from that width on. A click on it
    /// opens the command palette.
    /// </summary>
    public static TabButton? CommandsChip(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        return RightChips(tabs, active, width, keymap).Commands;
    }

    /// <summary>The text of the commands chip, <c> : commands </c>: the key comes from the keymap, the label stays.</summary>
    public static string CommandsChipText(KeymapTable? keymap = null)
    {
        var keys = keymap is null ? DefaultCommandsKey : keymap.FindKeyLabel(InputMode.Normal, ActionIds.PaletteOpen);

        return keys is null ? " commands " : $" {keys} commands ";
    }

    /// <summary>The text of the jump chip, <c> &lt;space&gt;g jump </c>: the keys come from the keymap, the label stays.</summary>
    public static string JumpChipText(KeymapTable? keymap = null)
    {
        var keys = keymap is null ? DefaultJumpLabel : keymap.FindKeyLabel(InputMode.Normal, ActionIds.IssueJump);

        return keys is null ? " jump " : $" {keys} jump ";
    }

    /// <summary>
    /// The bar exactly <paramref name="width"/> cells wide on the bar background: the tabs, then the jump chip
    /// right-aligned in the spare cells (left out when there is no room). Without tabs the bar is empty but still full.
    /// </summary>
    public static StyledLine BuildWithJumpChip(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null)
    {
        var line = Layout(tabs, active, width, keymap).Line;
        var chips = RightChips(tabs, active, width, keymap);
        var spans = new List<StyledSpan>(line.Spans);
        var column = DisplayWidth.Of(line.PlainText);

        if (chips.Commands is { } commands)
        {
            spans.Add(new StyledSpan(new string(' ', commands.Start - column), TextRole.BarText));
            spans.Add(new StyledSpan(CommandsChipText(keymap), TextRole.TabDim));
            column = commands.End;
        }

        if (chips.Jump is { } jump)
        {
            spans.Add(new StyledSpan(new string(' ', jump.Start - column), TextRole.BarText));
            spans.Add(new StyledSpan(JumpChipText(keymap), TextRole.TabDim));
        }

        var used = spans.Sum(span => DisplayWidth.Of(span.Text));

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), TextRole.BarText));
        }

        return new StyledLine(spans);
    }

    // The commands chip and the jump chip: the commands chip is kept whenever the bar is wide enough (the tabs make
    // room for it), the jump chip only when a spare cell separates it from the tabs and the commands chip.
    private static RightChipCells RightChips(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap)
    {
        var line = Layout(tabs, active, width, keymap).Line;
        var jumpWidth = DisplayWidth.Of(JumpChipText(keymap));
        var used = DisplayWidth.Of(line.PlainText);
        var hasJump = tabs.Count > 0;

        if (width < CommandsFrom)
        {
            var start = width - jumpWidth;

            return hasJump && start >= used + Separator.Length
                ? new RightChipCells(null, new TabButton(start, width))
                : new RightChipCells(null, null);
        }

        var commandsWidth = DisplayWidth.Of(CommandsChipText(keymap));
        var jumpStart = width - jumpWidth;
        var commandsStart = jumpStart - Separator.Length - commandsWidth;

        if (hasJump && commandsStart >= used + Separator.Length)
        {
            return new RightChipCells(
                new TabButton(commandsStart, commandsStart + commandsWidth),
                new TabButton(jumpStart, width));
        }

        return new RightChipCells(new TabButton(width - commandsWidth, width), null);
    }

    private static BarLayout Layout(IReadOnlyList<TabState> tabs, TabId? active, int width, KeymapTable? keymap)
    {
        if (tabs.Count == 0 || width <= 0)
        {
            return new BarLayout(StyledLine.Empty, [], null);
        }

        var activeIndex = Math.Max(0, IndexOf(tabs, active));
        var closeable = width >= CloseFrom;
        var compact = width < CompactBelow;

        if (width >= CommandsFrom)
        {
            width -= DisplayWidth.Of(CommandsChipText(keymap)) + Separator.Length;
        }

        if (compact)
        {
            return Window(tabs, activeIndex, width, closeable, marks: false);
        }

        var cap = FittingCap(tabs, activeIndex, width, closeable);

        if (cap == 0)
        {
            return Window(tabs, activeIndex, width, closeable, marks: true);
        }

        var texts = new string[tabs.Count];

        for (var index = 0; index < texts.Length; index++)
        {
            var isActive = index == activeIndex;

            texts[index] = ItemText(tabs[index], index, isActive ? ActiveTitleWidth : cap, isActive, closeable);
        }

        return Assemble(tabs, texts, activeIndex, 0, tabs.Count - 1, width, closeable, marks: true);
    }

    /// <summary>
    /// The largest inactive title width (24 down to 8) at which every tab fits, or 0 when not even 8 fits. Widths
    /// only grow with the cap, so the largest is found by bisection.
    /// </summary>
    private static int FittingCap(IReadOnlyList<TabState> tabs, int activeIndex, int width, bool closeable)
    {
        if (TotalWidth(tabs, activeIndex, InactiveTitleWidth, closeable) <= width)
        {
            return InactiveTitleWidth;
        }

        if (TotalWidth(tabs, activeIndex, MinimumTitleWidth, closeable) > width)
        {
            return 0;
        }

        var low = MinimumTitleWidth;
        var high = InactiveTitleWidth;

        while (high - low > 1)
        {
            var middle = (low + high) / 2;

            if (TotalWidth(tabs, activeIndex, middle, closeable) <= width)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static int TotalWidth(IReadOnlyList<TabState> tabs, int activeIndex, int cap, bool closeable)
    {
        var total = Separator.Length * (tabs.Count - 1);

        for (var index = 0; index < tabs.Count; index++)
        {
            var isActive = index == activeIndex;

            total += DisplayWidth.Of(ItemText(tabs[index], index, isActive ? ActiveTitleWidth : cap, isActive, closeable));
        }

        return total;
    }

    private static BarLayout Window(IReadOnlyList<TabState> tabs, int activeIndex, int width, bool closeable, bool marks)
    {
        var bare = DisplayWidth.Of(ItemText(tabs[activeIndex], activeIndex, 0, true, closeable));
        var hidden = 0;

        if (marks && activeIndex > 0)
        {
            hidden += Separator.Length + DisplayWidth.Of(HiddenLeft(activeIndex));
        }

        if (marks && activeIndex < tabs.Count - 1)
        {
            hidden += Separator.Length + DisplayWidth.Of(HiddenRight(tabs.Count - 1 - activeIndex));
        }

        var activeCap = Math.Clamp(width - bare - hidden, MinimumTitleWidth, ActiveTitleWidth);
        var texts = new string[tabs.Count];

        for (var index = 0; index < texts.Length; index++)
        {
            var isActive = index == activeIndex;

            texts[index] = ItemText(tabs[index], index, isActive ? activeCap : MinimumTitleWidth, isActive, closeable);
        }

        var low = activeIndex;
        var high = activeIndex;
        var grew = marks;

        while (grew)
        {
            grew = false;

            if (high + 1 < texts.Length && WindowWidth(texts, low, high + 1) <= width)
            {
                high++;
                grew = true;
            }

            if (low > 0 && WindowWidth(texts, low - 1, high) <= width)
            {
                low--;
                grew = true;
            }
        }

        return Assemble(tabs, texts, activeIndex, low, high, width, closeable, marks);
    }

    private static int WindowWidth(string[] texts, int low, int high)
    {
        var total = Separator.Length * (high - low);

        for (var index = low; index <= high; index++)
        {
            total += DisplayWidth.Of(texts[index]);
        }

        if (low > 0)
        {
            total += Separator.Length + DisplayWidth.Of(HiddenLeft(low));
        }

        if (high < texts.Length - 1)
        {
            total += Separator.Length + DisplayWidth.Of(HiddenRight(texts.Length - 1 - high));
        }

        return total;
    }

    private static BarLayout Assemble(
        IReadOnlyList<TabState> tabs,
        string[] texts,
        int activeIndex,
        int low,
        int high,
        int width,
        bool closeable,
        bool marks)
    {
        var spans = new List<StyledSpan>();
        var areas = new List<TabArea>(high - low + 1);
        var column = 0;

        if (marks && low > 0)
        {
            var mark = HiddenLeft(low);
            var markWidth = DisplayWidth.Of(mark);

            spans.Add(new StyledSpan(mark, TextRole.TabDim));
            spans.Add(new StyledSpan(Separator, TextRole.BarText));
            areas.Add(new TabArea(tabs[low - 1].Id, 0, markWidth));
            column = markWidth + Separator.Length;
        }

        for (var index = low; index <= high; index++)
        {
            if (index > low)
            {
                spans.Add(new StyledSpan(Separator, TextRole.BarText));
                column += Separator.Length;
            }

            var itemWidth = DisplayWidth.Of(texts[index]);
            var isActive = index == activeIndex;
            var close = CloseCells(isActive && closeable, column + itemWidth);

            spans.Add(new StyledSpan(texts[index], isActive ? TextRole.TabActive : TextRole.TabDim));
            areas.Add(new TabArea(tabs[index].Id, column, column + itemWidth, close));
            column += itemWidth;
        }

        if (marks && high < texts.Length - 1)
        {
            var mark = HiddenRight(texts.Length - 1 - high);

            spans.Add(new StyledSpan(Separator, TextRole.BarText));
            var markStart = column + Separator.Length;

            areas.Add(new TabArea(tabs[high + 1].Id, markStart, markStart + DisplayWidth.Of(mark)));
            spans.Add(new StyledSpan(mark, TextRole.TabDim));

            return new BarLayout(new StyledLine(spans), areas, null);
        }

        // The button only shows when the bar has a spare cell for it: it never pushes a tab out or past the width.
        if (column + Separator.Length + NewTabText.Length <= width)
        {
            spans.Add(new StyledSpan(Separator, TextRole.BarText));
            spans.Add(new StyledSpan(NewTabText, TextRole.BarMuted));

            var buttonStart = column + Separator.Length;

            return new BarLayout(new StyledLine(spans), areas, new TabButton(buttonStart, buttonStart + NewTabText.Length));
        }

        return new BarLayout(new StyledLine(spans), areas, null);
    }

    private static TabButton? CloseCells(bool shown, int chipEnd)
    {
        return shown ? new TabButton(chipEnd - CloseMark.Length, chipEnd) : null;
    }

    private static string HiddenLeft(int hidden)
    {
        return $" ‹{hidden} ";
    }

    private static string HiddenRight(int hidden)
    {
        return $" +{hidden} › ";
    }

    private static string ItemText(TabState tab, int index, int titleWidth, bool isActive, bool closeable)
    {
        var title = TabTitles.Cut(TabTitles.Label(tab.Definition), titleWidth);
        var close = isActive && closeable ? CloseMark : string.Empty;

        return $" {index + 1} {title}{Mark(tab)}{close} ";
    }

    private static string Mark(TabState tab)
    {
        return tab.Load switch
        {
            Loading => LoadingMark,
            Failed => FailedMark,
            _ => string.Empty,
        };
    }

    private static int IndexOf(IReadOnlyList<TabState> tabs, TabId? id)
    {
        for (var index = 0; index < tabs.Count; index++)
        {
            if (tabs[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record RightChipCells(TabButton? Commands, TabButton? Jump);

    private sealed record BarLayout(StyledLine Line, IReadOnlyList<TabArea> TabAreas, TabButton? NewTab);
}
