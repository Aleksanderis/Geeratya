using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The message history overlay: the last 50 status messages, newest first, scrolled like the help.</summary>
public sealed class MessagesBodyRenderer : IBodyRenderer
{
    private const string Title = "Messages";
    private const string SettingsTitle = "Settings";
    private const string EmptyText = "No messages yet.";

    public bool AppliesTo(AppState state)
    {
        return state.Mode == InputMode.Help && state.MessagesOpen;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var rows = new List<StyledLine>();

        if (state.MessagesShowSettings)
        {
            rows.AddRange(SettingsRegistry.ListLines(state.Settings).Select(line => StyledLine.Of(line)));
            rows.Add(StyledLine.Of(Title, TextRole.Heading));
        }

        if (state.StatusHistory.Count == 0)
        {
            rows.Add(StyledLine.Of(EmptyText, TextRole.Muted));
        }

        rows.AddRange(state.StatusHistory.Select(message => StyledLine.Of(message.Line, RoleOf(message.Severity))));

        var heading = state.MessagesShowSettings ? SettingsTitle : Title;

        return [StyledLine.Of(heading, TextRole.Heading), .. rows.Skip(state.HelpScroll)];
    }

    private static TextRole RoleOf(StatusSeverity severity)
    {
        return severity switch
        {
            StatusSeverity.Error => TextRole.Error,
            StatusSeverity.Warning => TextRole.Warning,
            _ => TextRole.Body,
        };
    }
}
