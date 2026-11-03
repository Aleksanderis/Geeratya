using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The start screen: title, active profile and the normal-mode hints, centred. Handles every state.</summary>
public sealed class StartBodyRenderer : IBodyRenderer
{
    private const string Title = "Geeratya";
    private const int BlockHeight = 3;
    private const string HintGap = "   ";

    public bool AppliesTo(AppState state)
    {
        return true;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        if (!FrameGrid.CanFrame(width, height))
        {
            return Block(state, context, width, height, true);
        }

        // The profile chip is the frame's title; the block inside keeps the profile line, connection and hints.
        IReadOnlyList<StyledSpan>? profile = null;

        if (state.Profiles.Active is { } active)
        {
            profile = [new StyledSpan(active.Name, TextRole.Muted)];
        }

        return PaneFrame.Build(
            width,
            height,
            [new StyledSpan(Title, TextRole.Heading)],
            profile,
            Block(state, context, width - 2, height - 2, false),
            []);
    }

    private static List<StyledLine> Block(AppState state, BodyContext context, int width, int height, bool withTitle)
    {
        var lines = Enumerable.Repeat(StyledLine.Empty, Math.Max(0, height)).ToList();
        var hints = string.Join(HintGap, StatusBar.HintsFor(InputMode.Normal, context.Keymap));
        var hint = Center(hints, width, TextRole.Muted);

        var connection = ConnectionText.Line(state.Session);
        var blockHeight = (connection.Length == 0 ? BlockHeight : BlockHeight + 1) - (withTitle ? 0 : 1);

        if (height >= blockHeight)
        {
            var top = (height - blockHeight) / 2;

            var next = top;

            if (withTitle)
            {
                lines[next++] = Center(Title, width, TextRole.Heading);
            }

            lines[next++] = Center(ProfileLine(state, context.Keymap), width, TextRole.Body);

            if (connection.Length > 0)
            {
                lines[next] = Center(connection, width, ConnectionRole(state.Session.Connection));
            }

            lines[top + blockHeight - 1] = hint;
        }
        else if (height > 0)
        {
            lines[0] = hint;
        }

        return lines;
    }

    private static TextRole ConnectionRole(ConnectionState connection)
    {
        return connection switch
        {
            ConnectionCheckFailure => TextRole.Error,
            Checking => TextRole.Muted,
            _ => TextRole.Body,
        };
    }

    private static string ProfileLine(AppState state, KeymapTable keymap)
    {
        if (state.Profiles.Active is { } active)
        {
            return $"{active.Name}: {active.Email} on {active.SiteUrl}";
        }

        var palette = keymap.FindKeyLabel(InputMode.Normal, ActionIds.PaletteOpen);

        return $"No profile yet: {palette}profile add";
    }

    private static StyledLine Center(string text, int width, TextRole role)
    {
        var fitted = TextWidth.Fit(text, width);

        return new StyledLine(
        [
            new StyledSpan(new string(' ', (width - TextWidth.Of(fitted)) / 2), TextRole.Body),
            new StyledSpan(fitted, role),
        ]);
    }
}
