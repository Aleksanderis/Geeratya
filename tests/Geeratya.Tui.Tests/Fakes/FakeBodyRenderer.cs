using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Fakes;

public sealed class FakeBodyRenderer : IBodyRenderer
{
    public bool AppliesTo(AppState state)
    {
        return true;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        return
        [
            new StyledLine(
            [
                new StyledSpan("> PROJ-1 ", TextRole.Selected),
                new StyledSpan("To Do", TextRole.StatusToDo),
            ]),
            new StyledLine(
            [
                new StyledSpan("  PROJ-2 ", TextRole.Muted),
                new StyledSpan("Done", TextRole.StatusDone),
            ]),
        ];
    }
}
