using Geeratya.Core.State;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Geeratya.Tui.Views;

/// <summary>The empty shell: body, status line and help overlay. Draws what <see cref="AppState"/> says.</summary>
public sealed class ShellScreen : Window
{
    private readonly Label _status;
    private readonly FrameView _helpFrame;
    private readonly Label _helpText;
    private readonly IReadOnlyList<string> _helpLines;

    public ShellScreen(IReadOnlyList<string> helpLines)
    {
        _helpLines = helpLines;
        Title = "Geeratya";
        Width = Dim.Fill();
        Height = Dim.Fill();

        var body = new Label
        {
            Text = "Geeratya\n\n? help   : commands   q quit",
            X = Pos.Center(),
            Y = Pos.Center(),
        };
        _status = new Label { Text = string.Empty, X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill() };
        _helpText = new Label { Text = string.Empty, X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _helpFrame = new FrameView
        {
            Title = "Help",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };

        _helpFrame.Add(_helpText);
        Add(body, _helpFrame, _status);
    }

    public void Render(AppState state)
    {
        _helpFrame.Visible = state.Mode == InputMode.Help;
        _helpText.Text = string.Join('\n', _helpLines.Skip(state.HelpScroll));
        _status.Text = StatusLine(state);
        SetNeedsDraw();
    }

    private static string StatusLine(AppState state)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Help => "-- HELP -- j/k scroll, ? or Esc closes",
            InputMode.Confirm => $"{state.Confirm?.Message} (y/n)",
            _ => state.Message ?? state.PendingKeys,
        };
    }
}
