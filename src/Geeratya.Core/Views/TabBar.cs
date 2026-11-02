using Geeratya.Core.Text;

namespace Geeratya.Core.Views;

/// <summary>
/// The tab bar line (<c>docs/ux-screens.md</c>, "Tab bar"). Pure layout: titles are cut (inactive to 24 cells, the
/// active one to 40), shrink towards 8 when the line is too short, and when even that does not fit a window of tabs
/// around the active one is shown with <c>‹n</c> and <c>+n ›</c> for the hidden ends.
/// </summary>
public static class TabBar
{
    /// <summary>Rows the tab bar takes at the top of the screen.</summary>
    public const int Rows = 1;

    /// <summary>The longest title of an inactive tab, in cells.</summary>
    public const int InactiveTitleWidth = 24;

    /// <summary>The longest title of the active tab, in cells.</summary>
    public const int ActiveTitleWidth = 40;

    /// <summary>The shortest title the bar shrinks to before it hides tabs.</summary>
    public const int MinimumTitleWidth = 8;

    private const string Separator = "  ";
    private const string Lead = " ";
    private const string LoadingMark = " …";
    private const string FailedMark = " !";

    /// <summary>
    /// The bar for <paramref name="tabs"/>; empty when there are none. It is at most <paramref name="width"/> cells
    /// wide unless the terminal is too narrow for even one tab; the frame cuts it then.
    /// </summary>
    public static StyledLine Build(IReadOnlyList<TabState> tabs, TabId? active, int width)
    {
        return Layout(tabs, active, width).Line;
    }

    /// <summary>
    /// The cells each drawn tab occupies in the bar built for <paramref name="width"/>, in the order drawn. A mouse
    /// click on a cell inside an area activates that tab; the separators and the hidden-tab marks are not areas.
    /// </summary>
    public static IReadOnlyList<TabArea> Areas(IReadOnlyList<TabState> tabs, TabId? active, int width)
    {
        return Layout(tabs, active, width).Areas;
    }

    private static (StyledLine Line, IReadOnlyList<TabArea> Areas) Layout(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width)
    {
        if (tabs.Count == 0 || width <= 0)
        {
            return (StyledLine.Empty, []);
        }

        var activeIndex = Math.Max(0, IndexOf(tabs, active));
        var cap = FittingCap(tabs, activeIndex, width);

        if (cap == 0)
        {
            return Window(tabs, activeIndex, width);
        }

        var texts = new string[tabs.Count];

        for (var index = 0; index < texts.Length; index++)
        {
            texts[index] = ItemText(tabs[index], index, index == activeIndex ? ActiveTitleWidth : cap, index == activeIndex);
        }

        return Assemble(tabs, texts, activeIndex, 0, tabs.Count - 1);
    }

    /// <summary>
    /// The largest inactive title width (24 down to 8) at which every tab fits, or 0 when not even 8 fits. Widths
    /// only grow with the cap, so the largest is found by bisection.
    /// </summary>
    private static int FittingCap(IReadOnlyList<TabState> tabs, int activeIndex, int width)
    {
        if (TotalWidth(tabs, activeIndex, InactiveTitleWidth) <= width)
        {
            return InactiveTitleWidth;
        }

        if (TotalWidth(tabs, activeIndex, MinimumTitleWidth) > width)
        {
            return 0;
        }

        var low = MinimumTitleWidth;
        var high = InactiveTitleWidth;

        while (high - low > 1)
        {
            var middle = (low + high) / 2;

            if (TotalWidth(tabs, activeIndex, middle) <= width)
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

    private static int TotalWidth(IReadOnlyList<TabState> tabs, int activeIndex, int cap)
    {
        var total = Lead.Length + (Separator.Length * (tabs.Count - 1));

        for (var index = 0; index < tabs.Count; index++)
        {
            var isActive = index == activeIndex;

            total += DisplayWidth.Of(ItemText(tabs[index], index, isActive ? ActiveTitleWidth : cap, isActive));
        }

        return total;
    }

    private static (StyledLine Line, IReadOnlyList<TabArea> Areas) Window(
        IReadOnlyList<TabState> tabs,
        int activeIndex,
        int width)
    {
        var bare = DisplayWidth.Of(ItemText(tabs[activeIndex], activeIndex, 0, true));
        var hidden = (activeIndex > 0 ? Separator.Length + HiddenLeft(activeIndex).Length : 0)
            + (activeIndex < tabs.Count - 1 ? Separator.Length + HiddenRight(tabs.Count - 1 - activeIndex).Length : 0);
        var activeCap = Math.Clamp(width - Lead.Length - bare - hidden, MinimumTitleWidth, ActiveTitleWidth);
        var texts = new string[tabs.Count];

        for (var index = 0; index < texts.Length; index++)
        {
            var isActive = index == activeIndex;

            texts[index] = ItemText(tabs[index], index, isActive ? activeCap : MinimumTitleWidth, isActive);
        }

        var low = activeIndex;
        var high = activeIndex;
        var grew = true;

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

        return Assemble(tabs, texts, activeIndex, low, high);
    }

    private static int WindowWidth(string[] texts, int low, int high)
    {
        var total = Lead.Length + (Separator.Length * (high - low));

        for (var index = low; index <= high; index++)
        {
            total += DisplayWidth.Of(texts[index]);
        }

        if (low > 0)
        {
            total += Separator.Length + HiddenLeft(low).Length;
        }

        if (high < texts.Length - 1)
        {
            total += Separator.Length + HiddenRight(texts.Length - 1 - high).Length;
        }

        return total;
    }

    private static (StyledLine Line, IReadOnlyList<TabArea> Areas) Assemble(
        IReadOnlyList<TabState> tabs,
        string[] texts,
        int activeIndex,
        int low,
        int high)
    {
        var spans = new List<StyledSpan> { new(Lead, TextRole.Body) };
        var areas = new List<TabArea>(high - low + 1);
        var column = DisplayWidth.Of(Lead);

        if (low > 0)
        {
            spans.Add(new StyledSpan(HiddenLeft(low), TextRole.Muted));
            spans.Add(new StyledSpan(Separator, TextRole.Body));
            column += DisplayWidth.Of(HiddenLeft(low)) + Separator.Length;
        }

        for (var index = low; index <= high; index++)
        {
            if (index > low)
            {
                spans.Add(new StyledSpan(Separator, TextRole.Body));
                column += Separator.Length;
            }

            var itemWidth = DisplayWidth.Of(texts[index]);

            spans.Add(new StyledSpan(texts[index], index == activeIndex ? TextRole.Selected : TextRole.Body));
            areas.Add(new TabArea(tabs[index].Id, column, column + itemWidth));
            column += itemWidth;
        }

        if (high < texts.Length - 1)
        {
            spans.Add(new StyledSpan(Separator, TextRole.Body));
            spans.Add(new StyledSpan(HiddenRight(texts.Length - 1 - high), TextRole.Muted));
        }

        return (new StyledLine(spans), areas);
    }

    private static string HiddenLeft(int hidden)
    {
        return $"‹{hidden}";
    }

    private static string HiddenRight(int hidden)
    {
        return $"+{hidden} ›";
    }

    private static string ItemText(TabState tab, int index, int titleWidth, bool isActive)
    {
        var title = TabTitles.Cut(TabTitles.For(tab.Definition), titleWidth);
        var text = $"{index + 1} {title}{Mark(tab)}";

        return isActive ? $"[{text}]" : text;
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
}
