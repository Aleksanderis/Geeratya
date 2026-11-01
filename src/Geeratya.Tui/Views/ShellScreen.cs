using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Size = System.Drawing.Size;

namespace Geeratya.Tui.Views;

/// <summary>
/// The shell window: draws the text frame of <see cref="FrameBuilder"/> in palette colours, and again on resize.
/// </summary>
public sealed class ShellScreen : Window
{
    private readonly Label _body;
    private readonly Label _status;
    private readonly IReadOnlyList<string> _helpLines;
    private AppState _state = AppState.Initial;
    private Size _size;

    public ShellScreen(IReadOnlyList<string> helpLines, ColorSupport colors, Size initialSize)
    {
        _helpLines = helpLines;
        _size = initialSize;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();
        SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Body)));

        _body = new Label { Text = string.Empty, X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        _status = new Label { Text = string.Empty, X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };
        _body.SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Body)));
        _status.SetScheme(CreateScheme(Palette.Resolve(colors, PaletteRole.Status)));

        Add(_body, _status);
        ViewportChanged += OnViewportChanged;
    }

    public void Render(AppState state)
    {
        _state = state;

        var lines = FrameBuilder.Build(state, _size.Width, _size.Height, _helpLines);

        if (lines.Count == 0)
        {
            return;
        }

        _body.Text = string.Join('\n', lines.Take(lines.Count - 1));
        _status.Text = lines[^1];
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
        var foreground = ToColor(style.Foreground16, style.Foreground);
        var background = ToColor(style.Background16, style.Background);

        return new Scheme(new Attribute(foreground, background, ToTextStyle(style.Style)));
    }

    private static Color ToColor(Ansi16Color? named, Rgb? rgb)
    {
        if (named is { } ansi)
        {
            return new Color((ColorName16)(int)ansi);
        }

        if (rgb is { } value)
        {
            return new Color(value.Red, value.Green, value.Blue);
        }

        return new Color(ColorName16.Gray);
    }

    private static TextStyle ToTextStyle(RoleStyle style)
    {
        var result = TextStyle.None;

        if (style.HasFlag(RoleStyle.Bold))
        {
            result |= TextStyle.Bold;
        }

        if (style.HasFlag(RoleStyle.Underline))
        {
            result |= TextStyle.Underline;
        }

        if (style.HasFlag(RoleStyle.Reverse))
        {
            result |= TextStyle.Reverse;
        }

        return result;
    }
}
