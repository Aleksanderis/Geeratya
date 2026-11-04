using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
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
        new MessagesBodyRenderer(),
        new IssueDetailRenderer(),
        new BacklogBodyRenderer(),
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

    /// <summary>The issue rows a list can show in <paramref name="height"/> terminal rows (never negative).</summary>
    public static int ListRows(int height)
    {
        return Math.Max(0, height - 1 - HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows);
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
        var helpOpen = HelpOverlay.Applies(state);
        var viewState = helpOpen ? HelpOverlay.Underlying(state) : state;
        var renderer = screens?.Concat(_defaultRenderers).First(candidate => candidate.AppliesTo(viewState))
            ?? _defaultRenderers.First(candidate => candidate.AppliesTo(viewState));
        var regions = new List<OverlayRegion>();
        var body = BodyOf(state, viewState, renderer, context, width, bodyHeight, regions);
        var settingsOpen = state is { SettingsScreen: not null, Mode: InputMode.Normal, ProfileList: null };
        var marker = helpOpen || settingsOpen || WhichKeyOverlay.Applies(state) ? null : RowMenuMarker.Stamp(body, state);

        if (helpOpen)
        {
            body = Fit(HelpOverlay.Compose(body, state, keymap, width, bodyHeight, regions), width, bodyHeight);
        }

        if (settingsOpen)
        {
            body = Fit(SettingsScreen.Compose(body, state, context, width, bodyHeight, regions), width, bodyHeight);
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

        var dialogShown = false;

        if (state.Mode == InputMode.Confirm && state.Confirm is not null)
        {
            var composed = ConfirmOverlay.Compose(body, state.Confirm, keymap, width, bodyHeight, regions);

            dialogShown = composed is not null;
            body = Fit(composed ?? body, width, bodyHeight);
        }

        if (state is { Mode: InputMode.Insert, Picker: null, Editor: null, Jump: null, Form: { } profileForm }
            && !FormReducer.IsOpen(state))
        {
            var composed = ProfileFormOverlay.Compose(body, profileForm, keymap, width, bodyHeight, regions);

            dialogShown = composed is not null;
            body = Fit(composed ?? body, width, bodyHeight);
        }

        if (WhichKeyOverlay.Applies(state) && state.Picker is null)
        {
            body = Fit(WhichKeyOverlay.Compose(body, state, keymap, width, bodyHeight), width, bodyHeight);
        }

        var status = StyledLines.Fit(StatusBar.RenderStyled(state, width, keymap, clock, dialogShown), width, TextRole.BarText);
        var frame = new StyledFrame(
            Fit(HeaderLines(state, width, clock, header, keymap), width, header),
            Fit(body, width, bodyHeight),
            status.PlainText)
        {
            StatusStyled = status,
            Regions = [.. regions.Select(region => region.Shifted(header))],
            RowMenuMarker = marker?.Shifted(header),
            StatusSegment = StatusBar.MessageSegment(state, width, clock),
        };

        return glyphs == GlyphSet.Ascii ? Asciified(frame) : frame;
    }

    private static List<StyledLine> BodyOf(
        AppState state,
        AppState viewState,
        IBodyRenderer renderer,
        BodyContext context,
        int width,
        int height,
        List<OverlayRegion> regions)
    {
        if (state.ProfileList is not null)
        {
            return ManagerBody(state, context, width, height, regions);
        }

        return Fit(renderer.Render(viewState, context, width, height), width, height);
    }

    // The profile manager is the whole body. Its rows and buttons take the mouse only while no form or question is on
    // top of it.
    private static List<StyledLine> ManagerBody(
        AppState state,
        BodyContext context,
        int width,
        int height,
        List<OverlayRegion> regions)
    {
        var interactive = state.Form is null && state.Mode != InputMode.Confirm;

        return Fit(ProfileManagerScreen.Render(state, context, width, height, interactive ? regions : null), width, height);
    }

    private static StyledFrame Asciified(StyledFrame frame)
    {
        var status = Asciified(frame.StatusStyled ?? StyledLine.Of(frame.StatusLine));

        return new StyledFrame(
            frame.Header.Select(Asciified).ToList(),
            frame.Body.Select(Asciified).ToList(),
            status.PlainText)
        {
            StatusStyled = status,
            Regions = frame.Regions,
            RowMenuMarker = frame.RowMenuMarker,
            StatusSegment = Asciified(frame.StatusSegment),
        };
    }

    private static StatusSegment? Asciified(StatusSegment? segment)
    {
        return segment is null ? null : segment with { Text = Glyphs.ToAscii(segment.Text) };
    }

    // The ASCII tier brackets chips (docs/ux-look.md, section 10): the padding cells of a chip become the brackets, so
    // the chip keeps its width and its mouse cells.
    private static StyledLine Asciified(StyledLine line)
    {
        return new StyledLine(line.Spans.Select(static span => span with { Text = Bracketed(span) }).ToArray());
    }

    private static string Bracketed(StyledSpan span)
    {
        var text = Glyphs.ToAscii(span.Text);
        var isChip = span.Role is TextRole.TabActive or TextRole.TabDim or TextRole.BarButton
            or TextRole.BadgeNormal or TextRole.BadgeVisual
            or TextRole.BadgeInsert or TextRole.BadgeCommand or TextRole.BadgeConfirm or TextRole.BadgeHelp;

        if (isChip && text.Length > 2 && text[0] == ' ' && text[^1] == ' ')
        {
            return $"[{text[1..^1]}]";
        }

        return text;
    }

    private static List<StyledLine> HeaderLines(
        AppState state,
        int width,
        DateTimeOffset now,
        int rows,
        KeymapTable keymap)
    {
        var lines = new List<StyledLine>(HeaderRows);

        if (rows > 0 && state.ProfileList is not null)
        {
            lines.Add(ProfilesChip(width));
        }
        else if (rows > 0)
        {
            lines.Add(TabBar.BuildWithJumpChip(state.Session.Tabs, state.Session.Active, width, keymap, state.HoveredTab));
        }

        if (rows > 1)
        {
            lines.Add(NetworkBanner.Line(state, now, width) ?? StyledLine.Empty);
        }

        return lines;
    }

    // The one chip of the tab row while the profile manager is open: it is not a tab of the active site.
    private static StyledLine ProfilesChip(int width)
    {
        return StyledLines.Fit(new StyledLine([new StyledSpan(" Profiles ", TextRole.TabActive)]), width, TextRole.BarText);
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
