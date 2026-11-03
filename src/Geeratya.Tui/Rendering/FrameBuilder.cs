using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Lays the shell out for a terminal size: a header region, a body from the first <see cref="IBodyRenderer"/> that handles
/// the state, and the status bar. Pure, so it is snapshot-tested and printed by <c>--print-frame</c> (plain text, styles dropped).
/// </summary>
public static class FrameBuilder
{
    private static readonly IReadOnlyList<IBodyRenderer> _defaultRenderers =
    [
        new HelpBodyRenderer(),
        new MessagesBodyRenderer(),
        new IssueDetailRenderer(),
        new ListBodyRenderer(),
        new ColumnsBodyRenderer(),
        new StartBodyRenderer(),
    ];

    /// <summary>The rows above the body: the tab bar and the network banner (blank while there is none).</summary>
    public const int HeaderRows = TabBar.Rows + 1;

    /// <summary>The rows the body gets in a terminal of <paramref name="height"/> rows (never negative).</summary>
    public static int BodyHeight(int height)
    {
        return Math.Max(0, height - 1 - Math.Min(HeaderRows, height - 1));
    }

    /// <summary>
    /// Exactly <paramref name="height"/> lines of exactly <paramref name="width"/> columns; empty for a size of zero.
    /// </summary>
    public static IReadOnlyList<string> Build(
        AppState state,
        int width,
        int height,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        return BuildStyled(state, width, height, keymap, null, now, glyphs)?.PlainLines() ?? [];
    }

    /// <summary>
    /// The styled frame; null for a size of zero. <paramref name="screens"/> are tried before the built-in help and
    /// start screens, in order. The first line is always the tab bar (blank without tabs), so the body height does not
    /// change when the first tab opens or the last one closes. The second line is the network banner of the active tab
    /// (blank when none), computed at <paramref name="now"/> (the epoch when null, which only the print frame uses).
    /// </summary>
    public static StyledFrame? BuildStyled(
        AppState state,
        int width,
        int height,
        KeymapTable keymap,
        IReadOnlyList<IBodyRenderer>? screens = null,
        DateTimeOffset? now = null,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var header = Math.Min(HeaderRows, height - 1);
        var clock = now ?? DateTimeOffset.UnixEpoch;
        var bodyHeight = BodyHeight(height);
        var context = new BodyContext(keymap, glyphs, clock);
        var renderer = screens?.Concat(_defaultRenderers).First(candidate => candidate.AppliesTo(state))
            ?? _defaultRenderers.First(candidate => candidate.AppliesTo(state));
        var body = Fit(renderer.Render(state, context, width, bodyHeight), width, bodyHeight);

        var regions = new List<OverlayRegion>();

        if (state.Mode == InputMode.Help && !state.MessagesOpen)
        {
            regions.AddRange(HelpBodyRenderer.PageRegions(state, keymap, width));
        }

        if (state.ProfileList is not null)
        {
            body = Fit(ProfileListOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (state.ContextMenu is not null)
        {
            body = Fit(ContextMenuOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (state.Mode == InputMode.Command)
        {
            body = Fit(PaletteOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (state.Picker is not null)
        {
            body = Fit(SourcePickerOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (state.Jump is not null)
        {
            body = Fit(JumpOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (state.Editor is not null)
        {
            var composed = CommentEditorOverlay.Compose(body, state, keymap, width, bodyHeight, regions);

            body = Fit(composed, width, bodyHeight);
        }

        if (FormReducer.OpenForm(state) is { } form)
        {
            body = Fit(FormOverlay.Compose(body, form, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        var confirmShown = false;

        if (state.Mode == InputMode.Confirm && state.Confirm is not null)
        {
            var composed = ConfirmOverlay.Compose(body, state.Confirm, keymap, width, bodyHeight, regions);

            confirmShown = composed is not null;
            body = Fit(composed ?? body, width, bodyHeight);
        }

        if (WhichKeyOverlay.Applies(state) && state.Picker is null)
        {
            body = Fit(WhichKeyOverlay.Compose(body, state, keymap, width, bodyHeight), width, bodyHeight);
        }

        var frame = new StyledFrame(
            Fit(HeaderLines(state, width, clock, header), width, header),
            Fit(body, width, bodyHeight),
            StatusBar.Render(state, width, keymap, clock, confirmShown))
        {
            Regions = [.. regions.Select(region => region.Shifted(header))],
            StatusSegment = StatusBar.MessageSegment(state, width, clock),
        };

        return glyphs == GlyphSet.Ascii ? Asciified(frame) : frame;
    }

    private static StyledFrame Asciified(StyledFrame frame)
    {
        return new StyledFrame(
            frame.Header.Select(Asciified).ToList(),
            frame.Body.Select(Asciified).ToList(),
            Glyphs.ToAscii(frame.StatusLine))
        {
            Regions = frame.Regions,
            StatusSegment = Asciified(frame.StatusSegment),
        };
    }

    private static StatusSegment? Asciified(StatusSegment? segment)
    {
        return segment is null ? null : segment with { Text = Glyphs.ToAscii(segment.Text) };
    }

    private static StyledLine Asciified(StyledLine line)
    {
        return new StyledLine(line.Spans.Select(static span => span with { Text = Glyphs.ToAscii(span.Text) }).ToArray());
    }

    private static List<StyledLine> HeaderLines(AppState state, int width, DateTimeOffset now, int rows)
    {
        var lines = new List<StyledLine>(HeaderRows);

        if (rows > 0)
        {
            lines.Add(TabBar.BuildWithJumpChip(state.Session.Tabs, state.Session.Active, width));
        }

        if (rows > 1)
        {
            lines.Add(NetworkBanner.Line(state, now, width) ?? StyledLine.Empty);
        }

        return lines;
    }

    private static List<StyledLine> Fit(IEnumerable<StyledLine> lines, int width, int height)
    {
        var fitted = lines.Take(height).Select(line => StyledLines.Fit(line, width)).ToList();

        while (fitted.Count < height)
        {
            fitted.Add(StyledLines.Fit(StyledLine.Empty, width));
        }

        return fitted;
    }
}
