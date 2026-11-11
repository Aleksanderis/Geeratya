using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;
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
    private readonly StyledBodyView _body;
    private readonly ColorSupport _colors;
    private readonly GlyphSet _glyphs;
    private readonly StyledBodyView _status;
    private readonly KeymapTable _keymap;
    private AppState _state = AppState.Initial;
    private Size _size;
    private Theme _baseTheme;
    private Theme _theme;
    private bool _transparent;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private IReadOnlyList<OverlayRegion> _regions = [];
    private OverlayRegion? _rowMenuMarker;
    private bool _dirty;

    public ShellScreen(
        KeymapTable keymap,
        ColorSupport colors,
        Size initialSize,
        Theme theme,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        _baseTheme = theme;
        _theme = theme;
        _keymap = keymap;
        _colors = colors;
        _glyphs = glyphs;
        _size = initialSize;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _body = new StyledBodyView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        _status = new StyledBodyView { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1 };
        ApplySchemes();

        Add(_body, _status);
        ViewportChanged += OnViewportChanged;
    }

    /// <summary>Raised when the body height changes after a resize; the host reports it as a viewport fact.</summary>
    public event Action<int>? BodyRowsChanged;

    /// <summary>
    /// The issue rows a list can show: the window height without the tab bar, the status line and the list's own frame rows
    /// (top edge with the title, column titles, bottom edge).
    /// </summary>
    public int BodyRows => FrameBuilder.ListRows(_size.Height);

    /// <summary>The rows the frame gives the body: where overlays (the Confirm dialog) are placed.</summary>
    public int BodyHeight => FrameBuilder.BodyHeight(_size.Height);

    /// <summary>The clickable rectangles of the overlay drawn last, for the mouse hit test.</summary>
    public IReadOnlyList<OverlayRegion> OverlayRegions
    {
        get
        {
            BuildFrame();

            return _regions;
        }
    }

    /// <summary>The cells of the row menu marker drawn last, null when none shows.</summary>
    public OverlayRegion? RowMenuMarker
    {
        get
        {
            BuildFrame();

            return _rowMenuMarker;
        }
    }

    /// <summary>How many frames were built; a burst of commands in one loop iteration builds one (#933).</summary>
    public int FramesBuilt { get; private set; }

    /// <summary>The terminal width, which issue details are laid out for.</summary>
    public int BodyColumns => Math.Max(0, _size.Width);

    /// <summary>Draws with another theme, starting with the next frame (drawn now).</summary>
    public void SetTheme(Theme theme)
    {
        _baseTheme = theme;
        _theme = theme.WithTerminalBackground(_transparent);
        ApplySchemes();
        Render(_state, _now);
    }

    /// <summary>
    /// Takes the state to show and schedules a draw. The frame itself is built once, when the draw (or a hit test)
    /// needs it, so several commands in one loop iteration cost one build.
    /// </summary>
    public void Render(AppState state, DateTimeOffset now)
    {
        _state = state;
        _now = now;
        _dirty = true;
        SetNeedsDraw();
    }

    /// <summary>Builds the frame of the latest state when it changed since the last build; does nothing otherwise.</summary>
    public void BuildFrame()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;

        var state = _state;
        var now = _now;

        if (state.Settings.TransparentBackground != _transparent)
        {
            _transparent = state.Settings.TransparentBackground;
            _theme = _baseTheme.WithTerminalBackground(_transparent);
            ApplySchemes();
        }

        var frame = FrameBuilder.BuildStyled(
            state,
            _size.Width,
            _size.Height,
            _keymap,
            null,
            now,
            _glyphs,
            state.Settings.LabelCaps && _colors != ColorSupport.None);

        if (frame is null)
        {
            return;
        }

        _regions = frame.Regions;
        _rowMenuMarker = frame.RowMenuMarker;
        var faint = _glyphs != GlyphSet.Ascii;
        var lines = frame.Header.Concat(frame.Body).Select(line => StyledRuns.Resolve(line, _theme, _colors, faint));

        _body.SetLines(lines.ToList());
        _status.SetLines([StyledRuns.Resolve(frame.StatusStyled ?? StyledLine.Of(frame.StatusLine), _theme, _colors)]);
        FramesBuilt++;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        BuildFrame();

        return base.OnDrawingContent(context);
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
