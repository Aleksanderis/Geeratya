using Geeratya.Core.Keymap;
using Geeratya.Core.Text;

namespace Geeratya.Core.Views;

// The chips at the right end of the bar: the commands chip and the jump chip.
public static partial class TabBar
{
    /// <summary>
    /// The cells of the jump chip at the right end of the bar, null when there are no tabs or the bar leaves
    /// no room (a spare cell between the last tab item or <c>+</c> and the chip). A click on it opens the
    /// jump prompt. <paramref name="keymap"/> names the keys on the chip (the default binding without it).
    /// </summary>
    public static TabButton? JumpChip(
        IReadOnlyList<TabState> tabs,
        TabId? active,
        int width,
        KeymapTable? keymap = null,
        TabId? hovered = null)
    {
        return RightChips(tabs, active, width, keymap, hovered).Jump;
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
        KeymapTable? keymap = null,
        TabId? hovered = null)
    {
        return RightChips(tabs, active, width, keymap, hovered).Commands;
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
        KeymapTable? keymap = null,
        TabId? hovered = null)
    {
        var line = Layout(tabs, active, width, keymap, hovered).Line;
        var chips = RightChips(tabs, active, width, keymap, hovered);
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
        KeymapTable? keymap,
        TabId? hovered)
    {
        var line = Layout(tabs, active, width, keymap, hovered).Line;
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

    private sealed record RightChipCells(TabButton? Commands, TabButton? Jump);
}
