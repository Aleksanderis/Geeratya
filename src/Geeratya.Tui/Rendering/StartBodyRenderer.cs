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
        var lines = Enumerable.Repeat(StyledLine.Empty, Math.Max(0, height)).ToList();
        var hints = string.Join(HintGap, StatusBar.HintsFor(InputMode.Normal, context.Keymap));
        var hint = Center(hints, width, TextRole.Muted);

        if (height >= BlockHeight)
        {
            var top = (height - BlockHeight) / 2;

            lines[top] = Center(Title, width, TextRole.Heading);
            lines[top + 1] = Center(ProfileLine(state, context.Keymap), width, TextRole.Body);
            lines[top + 2] = hint;
        }
        else if (height > 0)
        {
            lines[0] = hint;
        }

        return lines;
    }

    private static string ProfileLine(AppState state, KeymapTable keymap)
    {
        if (state.Profiles.Active is { } active)
        {
            return $"{active.Name}: {active.Email} on {active.SiteUrl}";
        }

        var palette = keymap.FindKeys(InputMode.Normal, ActionIds.PaletteOpen);

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
