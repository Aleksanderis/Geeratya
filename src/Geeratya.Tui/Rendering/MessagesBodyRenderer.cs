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
            rows.AddRange(SettingsRegistry.ListLines(state.Settings).Select(line => StyledLine.Of(Fit(line, width))));
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

    /// <summary>Cuts <paramref name="line"/> to <paramref name="width"/> cells and ends it with an ellipsis, so a cut is visible.</summary>
    private static string Fit(string line, int width)
    {
        if (DisplayWidth.Of(line) <= width)
        {
            return line;
        }

        var cut = new System.Text.StringBuilder();
        var used = 0;

        foreach (var rune in line.EnumerateRunes())
        {
            var cells = DisplayWidth.Of(rune);

            if (used + cells > width - 1)
            {
                break;
            }

            cut.Append(rune.ToString());
            used += cells;
        }

        return cut + "\u2026";
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
