using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.Text;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static partial class StatusBar
{
    private const string HintGap = "  ";
    private const string Separator = " · ";
    private const string PickerPrompt = "open a view";
    private const string SprintPrompt = "switch sprint";
    private const string MovePrompt = "move to sprint";
    private const string JumpPrompt = "jump to an issue";
    private const int MinViewRoom = 8;
    private const int MaxViewReserve = 40;

    /// <summary>
    /// The status line for <paramref name="state"/>, exactly <paramref name="width"/> columns wide.
    /// <paramref name="now"/> is the clock the network word counts from (a rate-limit wait); without it the epoch is used.
    /// <paramref name="confirmDialogShown"/> says the Confirm dialog is drawn, so the line keeps only the short
    /// <c>Confirm  (y/n)</c>; otherwise the question itself stays in the line.
    /// </summary>
    public static string Render(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        bool confirmDialogShown = false)
    {
        return RenderStyled(state, width, keymap, now, confirmDialogShown).PlainText;
    }

    /// <summary>
    /// The status line as styled spans (<c>docs/ux-look.md</c>, section 5): the mode badge as a filled chip at column
    /// 0, the text (a message in its severity role), then the hint chips right-aligned, the key bold and the label
    /// muted. Its plain text is <see cref="Render"/>; it is exactly <paramref name="width"/> columns wide.
    /// </summary>
    public static StyledLine RenderStyled(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        bool confirmDialogShown = false)
    {
        if (width <= 0)
        {
            return StyledLine.Empty;
        }

        var clock = now ?? DateTimeOffset.UnixEpoch;
        var text = Text(state, width, keymap, clock, confirmDialogShown, 0);
        // The goal never asks for room: it shows only when it fits whole, after the rest.
        var wanted = TextWidth.Of(Text(state, width, keymap, clock, confirmDialogShown, int.MaxValue, false, false))
            - TextWidth.Of(text);

        // The view text (source, count, filter and lane, breadcrumb, ruler) is state the user needs: up to
        // MaxViewReserve cells are kept for it and the screen hints drop from the end to make room (the global
        // hints stay while they fit).
        var reserve = Math.Min(Math.Max(0, wanted), MaxViewReserve);
        var left = Left(state, text);
        var room = width - TextWidth.Of(left);
        var hints = ShownHints(state, keymap, room - reserve);

        // The global hints never give way to the view text: shrink the reserve until they fit again.
        while (hints.Count == 0 && reserve > 0 && ShownHints(state, keymap, room).Count > 0)
        {
            reserve--;
            hints = ShownHints(state, keymap, room - reserve);
        }

        var hintWidth = HintBar.Width(hints);
        var spare = width - TextWidth.Of(left) - hintWidth - Separator.Length;

        if (hints.Count > 0)
        {
            spare -= HintGap.Length;
        }

        var shown = wanted > 0 && spare >= MinViewRoom
            ? Text(state, width, keymap, clock, confirmDialogShown, spare)
            : text;

        if (shown != text)
        {
            left = Left(state, shown);
        }

        // The selected issue is last in line: it shows only when the line, the hints and their gap still fit.
        var viewRoom = shown == text ? 0 : StatusViewText.PositionedRoom(state, spare);
        var positioned = Text(state, width, keymap, clock, confirmDialogShown, viewRoom, true);

        if (positioned != shown && !StatusViewText.LosesGoal(state, shown, positioned))
        {
            var positionedLeft = Left(state, positioned);
            var needed = TextWidth.Of(positionedLeft) + hintWidth + (hints.Count > 0 ? HintGap.Length : 0);

            if (needed <= width)
            {
                shown = positioned;
                left = positionedLeft;
            }
        }

        var gap = width - TextWidth.Of(left) - hintWidth;
        var spans = new List<StyledSpan> { new($" {Badge(state)} ", BadgeRole(state)) };

        if (shown.TrimEnd().Length > 0)
        {
            spans.Add(new StyledSpan(" ", TextRole.BarText));
            spans.Add(new StyledSpan(shown.TrimEnd(), MessageRole(state, width, clock)));
        }

        if (hints.Count == 0 || gap < HintGap.Length)
        {
            return StyledLines.Fit(new StyledLine(spans), width, TextRole.BarText);
        }

        spans.Add(new StyledSpan(new string(' ', gap), TextRole.BarText));
        spans.AddRange(HintBar.Spans(hints));

        return StyledLines.Fit(new StyledLine(spans), width, TextRole.BarText);
    }

    private static string Left(AppState state, string text)
    {
        var chip = $" {Badge(state)} ";

        return text.TrimEnd().Length == 0 ? chip : $"{chip} {text.TrimEnd()}";
    }

    private static TextRole MessageRole(AppState state, int width, DateTimeOffset clock)
    {
        return MessageSegment(state, width, clock)?.Role switch
        {
            PaletteRole.Error => TextRole.BarError,
            PaletteRole.Warning => TextRole.BarWarning,
            PaletteRole.SuccessChip => TextRole.BarSuccessChip,
            PaletteRole.Success => TextRole.BarSuccess,
            _ => TextRole.BarText,
        };
    }

    /// <summary>
    /// The hints that fit in <paramref name="room"/>. The global ones are kept first; the screen hints before them are
    /// added in order while they fit (a gap to the text is kept), so a narrow line loses the screen hints from the
    /// end, one by one, and the global ones last.
    /// </summary>
    private static IReadOnlyList<HintChip> Fit(HintList hints, int room)
    {
        var used = HintBar.Width(hints.Global);

        if (used == 0 || used + HintGap.Length > room)
        {
            return [];
        }

        var screen = new List<HintChip>();

        foreach (var chip in hints.Screen)
        {
            var next = used + chip.Width + HintGap.Length;

            if (next + HintGap.Length > room)
            {
                break;
            }

            screen.Add(chip);
            used = next;
        }

        return [.. screen, .. hints.Global];
    }

    private static IReadOnlyList<HintChip> ShownHints(AppState state, KeymapTable keymap, int room)
    {
        if (state.Settings.StatusHints == HintLevel.Off)
        {
            return [];
        }

        return Fit(Leveled(HintTexts(state, keymap), state), room);
    }

    /// <summary>Minimal keeps only the three global hints of Normal mode (<c>? help  : commands  q quit</c>).</summary>
    private static HintList Leveled(HintList hints, AppState state)
    {
        if (state.Settings.StatusHints != HintLevel.Minimal || state.Mode != InputMode.Normal)
        {
            return hints;
        }

        return new HintList([], hints.Global);
    }

    private readonly record struct HintList(IReadOnlyList<HintChip> Screen, IReadOnlyList<HintChip> Global);

    /// <summary>
    /// The message to paint in its severity role inside the bar, or null. Info is drawn in the success role and, for the
    /// first <see cref="StatusMessages.FreshFor"/> after it was posted, as a filled chip (bold reverse, so the
    /// no-colour tier shows it too); warnings and errors keep their roles.
    /// </summary>
    public static StatusSegment? MessageSegment(AppState state, int width, DateTimeOffset? now = null)
    {
        if (StatusMessages.Cells(state) is not { } cells || state.Status is not { } status || cells.Start >= width)
        {
            return null;
        }

        var role = status.Severity switch
        {
            StatusSeverity.Error => PaletteRole.Error,
            StatusSeverity.Warning => PaletteRole.Warning,
            _ => now is { } at && StatusMessages.IsFresh(state, at) ? PaletteRole.SuccessChip : PaletteRole.Success,
        };

        return new StatusSegment(cells.Start, Math.Min(cells.End, width) - cells.Start, status.Line, role);
    }
}
