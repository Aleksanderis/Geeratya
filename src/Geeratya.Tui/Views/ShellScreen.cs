using System.Text;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Size = System.Drawing.Size;

namespace Geeratya.Tui.Views;

/// <summary>
/// The shell window: draws the text frame of <see cref="FrameBuilder"/> in palette colours, and again on resize.
/// </summary>
public sealed class ShellScreen : Window
{
    // Typed text such as an underscore in a command must show as typed, so no character is a hot key marker.
    private static readonly Rune _noHotKey = new(0xFFFF);

    private readonly StyledBodyView _body;
    private readonly ColorSupport _colors;
    private readonly GlyphSet _glyphs;
    private readonly Label _status;
    private IReadOnlyList<string> _helpLines;
    private readonly KeymapTable _keymap;
    private AppState _state = AppState.Initial;
    private Size _size;
    private Theme _theme;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private IReadOnlyList<OverlayRegion> _regions = [];

    public ShellScreen(
        KeymapTable keymap,
        ColorSupport colors,
        Size initialSize,
        Theme theme,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        _theme = theme;
        _keymap = keymap;
        _colors = colors;
        _glyphs = glyphs;
        _helpLines = KeymapDocument.RenderHelpLines(keymap);
        _size = initialSize;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _body = new StyledBodyView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        _status = new Label { Text = string.Empty, X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), HotKeySpecifier = _noHotKey };
        ApplySchemes();

        Add(_body, _status);
        ViewportChanged += OnViewportChanged;
    }

    /// <summary>Raised when the body height changes after a resize; the host reports it as a viewport fact.</summary>
    public event Action<int>? BodyRowsChanged;

    /// <summary>
    /// The issue rows a list can show: the window height without the tab bar, the status line and the list's own header rows
    /// (title with count, column titles).
    /// </summary>
    public int BodyRows => Math.Max(0, _size.Height - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows);

    /// <summary>The clickable rectangles of the overlay drawn last, for the mouse hit test.</summary>
    public IReadOnlyList<OverlayRegion> OverlayRegions => _regions;

    /// <summary>The terminal width, which issue details are laid out for.</summary>
    public int BodyColumns => Math.Max(0, _size.Width);

    /// <summary>Builds the help text again after the keymap was replaced.</summary>
    public void RefreshHelp()
    {
        _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    }

    /// <summary>Draws with another theme, starting with the next frame (drawn now).</summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        ApplySchemes();
        Render(_state, _now);
    }

    public void Render(AppState state, DateTimeOffset now)
    {
        _state = state;
        _now = now;

        var frame = FrameBuilder.BuildStyled(state, _size.Width, _size.Height, _helpLines, _keymap, null, now, _glyphs);

        if (frame is null)
        {
            return;
        }

        _regions = frame.Regions;
        var lines = frame.Header.Concat(frame.Body).Select(line => StyledRuns.Resolve(line, _theme, _colors));

        _body.SetLines(lines.ToList());
        _status.Text = frame.StatusLine;
        SetNeedsDraw();
    }

    private void OnViewportChanged(object? sender, DrawEventArgs eventArgs)
    {
        var size = Viewport.Size;

        if (size.Width <= 0 || size.Height <= 0 || size == _size)
        {
            return;
        }

        _size = size;
        Render(_state, _now);
        BodyRowsChanged?.Invoke(BodyRows);
    }

    private void ApplySchemes()
    {
        var body = CreateScheme(Palette.Resolve(_theme, _colors, PaletteRole.Body));

        SetScheme(body);
        _body.SetScheme(body);
        _status.SetScheme(CreateScheme(Palette.Resolve(_theme, _colors, PaletteRole.Status)));
    }

    private static Scheme CreateScheme(ResolvedStyle style)
    {
        return new Scheme(AttributeFactory.Create(style));
    }
}
