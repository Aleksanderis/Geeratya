using System.Text;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
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
    private readonly Label _status;
    private readonly IReadOnlyList<string> _helpLines;
    private readonly KeymapTable _keymap;
    private AppState _state = AppState.Initial;
    private Size _size;

    public ShellScreen(KeymapTable keymap, ColorSupport colors, Size initialSize)
    {
        _keymap = keymap;
        _colors = colors;
        _helpLines = KeymapDocument.RenderHelpLines(keymap);
        _size = initialSize;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();
        SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Body)));

        _body = new StyledBodyView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        _status = new Label { Text = string.Empty, X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), HotKeySpecifier = _noHotKey };
        _body.SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Body)));
        _status.SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Status)));

        Add(_body, _status);
        ViewportChanged += OnViewportChanged;
    }

    public void Render(AppState state)
    {
        _state = state;

        var frame = FrameBuilder.BuildStyled(state, _size.Width, _size.Height, _helpLines, _keymap);

        if (frame is null)
        {
            return;
        }

        _body.SetLines(frame.Header.Concat(frame.Body).Select(line => StyledRuns.Resolve(line, _colors)).ToList());
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
        Render(_state);
    }

    private static Scheme CreateScheme(ResolvedStyle style)
    {
        return new Scheme(AttributeFactory.Create(style));
    }
}
