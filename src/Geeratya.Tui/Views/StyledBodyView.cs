using Geeratya.Tui.Rendering;
using Terminal.Gui.ViewBase;

namespace Geeratya.Tui.Views;

/// <summary>Draws styled lines span by span; no character is a hot key marker and wide characters are never split.</summary>
public sealed class StyledBodyView : View
{
    private IReadOnlyList<IReadOnlyList<StyledRun>> _lines = [];

    public void SetLines(IReadOnlyList<IReadOnlyList<StyledRun>> lines)
    {
        _lines = lines;
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        for (var row = 0; row < _lines.Count && row < Viewport.Height; row++)
        {
            Move(0, row);

            foreach (var run in _lines[row])
            {
                SetAttribute(AttributeFactory.Create(run.Style));
                AddStr(run.Text);
            }
        }

        return true;
    }
}
