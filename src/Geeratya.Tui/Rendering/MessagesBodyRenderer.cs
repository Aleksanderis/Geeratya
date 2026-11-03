using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The message history overlay: the last 50 status messages, newest first, scrolled like the help.</summary>
public sealed class MessagesBodyRenderer : IBodyRenderer
{
    private const string Title = "Messages";
    private const string EmptyText = "No messages yet.";

    public bool AppliesTo(AppState state)
    {
        return state.Mode == InputMode.Help && state.MessagesOpen;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var lines = new List<StyledLine> { StyledLine.Of(Title, TextRole.Heading) };

        if (state.StatusHistory.Count == 0)
        {
            lines.Add(StyledLine.Of(EmptyText, TextRole.Muted));

            return lines;
        }

        foreach (var message in state.StatusHistory.Skip(state.HelpScroll))
        {
            lines.Add(StyledLine.Of(message.Line, RoleOf(message.Severity)));
        }

        return lines;
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
