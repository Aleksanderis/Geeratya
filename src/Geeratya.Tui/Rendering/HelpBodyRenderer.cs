using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The help overlay: a title and the generated key reference, scrolled.</summary>
public sealed class HelpBodyRenderer : IBodyRenderer
{
    private const string Title = "Help";

    public bool AppliesTo(AppState state)
    {
        return state.Mode == InputMode.Help;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var lines = new List<StyledLine> { StyledLine.Of(Title, TextRole.Heading) };

        lines.AddRange(context.HelpLines.Skip(state.HelpScroll).Select(line => StyledLine.Of(line)));

        return lines;
    }
}
