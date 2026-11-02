using Geeratya.Core.Keymap;
using Geeratya.Core.Forms;
using Geeratya.Core.State;

namespace Geeratya.Core.Mouse;

/// <summary>What the hit test needs to know about the screen: its size and what covers the body.</summary>
/// <param name="Width">Terminal width in cells.</param>
/// <param name="Height">Terminal height in rows.</param>
/// <param name="HeaderRows">Rows above the body (tab bar and banner).</param>
/// <param name="OverlayOpen">A prompt, picker, form, editor, help or confirm covers the body.</param>
/// <param name="DetailOpen">An issue is opened in the active tab.</param>
public sealed record MouseContext(int Width, int Height, int HeaderRows, bool OverlayOpen, bool DetailOpen)
{
    public static MouseContext From(AppState state, int width, int height, int headerRows)
    {
        var tab = state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id) : null;
        var overlay = state.Mode != InputMode.Normal || state.Picker is not null || state.Jump is not null
            || state.Editor is not null || FormReducer.IsOpen(state);

        return new MouseContext(width, height, headerRows, overlay, tab is { Detail.Count: > 0 });
    }
}
