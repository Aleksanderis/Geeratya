using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Tui.Input;
using Geeratya.Tui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Geeratya.Tui;

/// <summary>
/// Runs the Terminal.Gui loop and connects it to the state layer: keys become commands, state becomes a frame.
/// Nothing here waits on the network; the first frame is drawn before any Jira call exists.
/// </summary>
public sealed class ShellHost
{
    private readonly KeymapTable _keymap;
    private readonly AppReducer _reducer;
    private IApplication? _application;
    private ShellScreen? _screen;
    private AppState _state = AppState.Initial;

    public ShellHost(KeymapTable keymap)
    {
        _keymap = keymap;
        _reducer = new AppReducer(keymap);
    }

    public int Run()
    {
        using var application = Application.Create();

        _application = application;
        application.Init();
        application.Keyboard.KeyBindings.Clear();
        application.Keyboard.KeyDown += OnKeyDown;

        using var screen = new ShellScreen(KeymapDocument.RenderHelpLines(_keymap));

        _screen = screen;
        screen.Render(_state);
        application.Run(screen);

        return 0;
    }

    private void OnKeyDown(object? sender, Key key)
    {
        var spelled = KeyTranslator.Translate(key);

        if (spelled is null)
        {
            return;
        }

        key.Handled = true;

        var result = _reducer.Reduce(_state, new KeyPressed(spelled));

        _state = result.State;
        _screen!.Render(_state);

        if (result.Effects.OfType<QuitEffect>().Any())
        {
            _application!.RequestStop();
        }
    }
}
