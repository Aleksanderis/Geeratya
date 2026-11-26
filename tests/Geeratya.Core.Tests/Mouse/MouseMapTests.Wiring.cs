using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Mouse;

/// <summary>
/// The wiring check of the mouse table: every row of a wired part must produce a command when the resolver is given
/// the gesture on that target, so a row cannot sit in the table and the document without doing anything. Rows of a
/// part that is not wired must say so in the document.
/// </summary>
public partial class MouseMapTests
{
    private const int Width = 100;
    private const int Height = 30;
    private const int HeaderRows = 2;
    private const int ProbeRow = 10;
    private const int ProbeColumn = 20;
    private const string NotWired = "Not wired.";

    private static readonly DateTimeOffset _at = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenEveryWiredRow_WhenResolvingItsGestureOnItsTarget_ThenTheResolverYieldsACommand()
    {
        var silent = new List<string>();

        foreach (var row in DefaultMouseMap.Rows.Where(static row => MouseResolver.WiredParts.Contains(row.Part)))
        {
            if (Probe(row).Count == 0)
            {
                silent.Add($"{row.Target} {row.Gesture}");
            }
        }

        Assert.True(silent.Count == 0, $"Rows in the mouse table that nothing resolves: {string.Join(", ", silent)}");
    }

    [Fact]
    public void GivenAProbeOfARowWithAnAction_WhenResolving_ThenTheCommandCarriesThatAction()
    {
        var wrong = new List<string>();

        foreach (var row in DefaultMouseMap.Rows.Where(static row => MouseResolver.WiredParts.Contains(row.Part)))
        {
            var actions = Probe(row).OfType<ActionRequested>().Select(static request => request.Action).ToList();

            if (actions.Count > 0 && !actions.Contains(row.Action))
            {
                wrong.Add($"{row.Target} {row.Gesture}: {string.Join(", ", actions)}");
            }
        }

        Assert.True(wrong.Count == 0, $"Rows whose command runs another action: {string.Join("; ", wrong)}");
    }

    [Fact]
    public void GivenTheDocument_WhenReadingTheWiredFlag_ThenOnlyRowsOfUnwiredPartsSayNotWired()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs", "internal", "ux-mouse.md");
        var wrong = new List<string>();

        foreach (var line in File.ReadAllLines(path).Where(static line => line.StartsWith("| ", StringComparison.Ordinal)))
        {
            var cells = line.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray();

            if (cells.Length < 5 || cells[3].Length != 1)
            {
                continue;
            }

            var wired = MouseResolver.WiredParts.Contains(cells[3][0]);
            var says = cells[4].StartsWith(NotWired, StringComparison.Ordinal);

            if (wired == says)
            {
                wrong.Add($"{cells[0]} {cells[1]}");
            }
        }

        Assert.True(wrong.Count == 0, $"Rows whose document text and part disagree on wiring: {string.Join(", ", wrong)}");
    }

    private static IReadOnlyList<Command> Probe(MouseRow row)
    {
        var context = ContextFor(row);

        if (row.Gesture is MouseGesture.Drag or MouseGesture.DragRows)
        {
            return MouseResolver.ResolveDrag(context, ProbeColumn, ProbeRow, ProbeColumn, ProbeRow + 2, _at);
        }

        if (row.Gesture is MouseGesture.DragToSprint or MouseGesture.DragToBacklog)
        {
            // A drop onto a sprint or the backlog is a drag of rows; the reducer decides where it was released.
            return MouseResolver.ResolveDrag(context, ProbeColumn, ProbeRow, ProbeColumn, ProbeRow + 2, _at);
        }

        var cell = CellOf(row, context);

        return MouseResolver.Resolve(context, cell.Column, cell.Row, row.Gesture, _at);
    }

    private static MouseContext ContextFor(MouseRow row)
    {
        var target = row.Target;
        var detail = target == MouseTarget.Detail;
        var context = new MouseContext(Width, Height, HeaderRows, false, detail)
        {
            ColumnsLayout = row.Gesture == MouseGesture.Drag,
            ActiveTab = new TabId(1),
        };

        if (row.Gesture is MouseGesture.DragToSprint or MouseGesture.DragToBacklog)
        {
            return context with { ColumnsLayout = false };
        }

        return target switch
        {
            MouseTarget.Tabs => context with { TabAreas = [new TabArea(new TabId(1), 0, 10)] },
            MouseTarget.TabClose => context with { TabAreas = [new TabArea(new TabId(1), 0, 10, new TabButton(8, 10))] },
            MouseTarget.TabOverflow => context with { TabAreas = [new TabArea(new TabId(1), 0, 10, null, true)] },
            MouseTarget.NewTabButton => context with { NewTabButton = new TabButton(0, 5) },
            MouseTarget.CommandsChip => context with { CommandsChip = new TabButton(0, 5) },
            MouseTarget.JumpChip => context with { JumpChip = new TabButton(0, 5) },
            MouseTarget.LaneChip => context with { LaneChip = new TabButton(0, 20) },
            MouseTarget.Banner => context with { BannerShown = true },
            MouseTarget.StatusMessage => context with { StatusMessage = new TabButton(0, 20) },
            MouseTarget.RowMenuButton => context with { RowMenuMarker = Region(target, 0) },
            MouseTarget.Lists or MouseTarget.Detail or MouseTarget.Status => context,
            // A click on a sub-task row is a placed pointer; the reducer then toggles the list (AppReducer.Pointer).
            MouseTarget.SubtaskRow => context,
            MouseTarget.Backdrop => context with { OverlayOpen = true },
            _ => OverlayContext(context, row),
        };
    }

    // An overlay with one box and one row of the target; a wheel row also names the overlay that takes the wheel.
    private static MouseContext OverlayContext(MouseContext context, MouseRow row)
    {
        var box = Region(MouseTarget.OverlayBox, 0) with { Top = ProbeRow - 1, Height = 5, Width = 60 };
        var target = Region(row.Target, 0);

        // The text of a text area spans rows, so a drag can start on one and end on another.
        var rows = row.Target == MouseTarget.EditorText ? [target, target with { Top = ProbeRow + 2, Line = 2 }] : new[] { target };
        var wheeling = row.Gesture is MouseGesture.WheelUp or MouseGesture.WheelDown;

        return context with
        {
            OverlayOpen = true,
            ConfirmOpen = row.Target is MouseTarget.ConfirmYes or MouseTarget.ConfirmNo,
            Regions = [box, .. rows],
            WheelTarget = wheeling ? row.Target : null,
            WheelScope = ScopeOf(row.Target),
        };
    }

    private static WheelScope ScopeOf(MouseTarget target)
    {
        return target switch
        {
            MouseTarget.ContextMenu or MouseTarget.HelpBody or MouseTarget.MessagesBody => WheelScope.Screen,
            MouseTarget.FormChoice or MouseTarget.EditorChoice => WheelScope.Rows,
            _ => WheelScope.Box,
        };
    }

    private static Cell CellOf(MouseRow row, MouseContext context)
    {
        if (context.OverlayOpen && row.Target != MouseTarget.Backdrop)
        {
            return new Cell(ProbeColumn, ProbeRow);
        }

        return row.Target switch
        {
            MouseTarget.Tabs or MouseTarget.NewTabButton or MouseTarget.CommandsChip or MouseTarget.JumpChip
                => new Cell(3, 0),
            MouseTarget.TabClose or MouseTarget.TabOverflow => new Cell(9, 0),
            MouseTarget.Banner => new Cell(5, 1),
            MouseTarget.Status or MouseTarget.StatusMessage or MouseTarget.LaneChip => new Cell(3, Height - 1),
            MouseTarget.Backdrop => new Cell(3, 0),
            _ => new Cell(ProbeColumn, ProbeRow),
        };
    }

    private static OverlayRegion Region(MouseTarget target, int index)
    {
        return new OverlayRegion(target, ProbeRow, ProbeColumn - 5, 30, 1, index);
    }

    private readonly record struct Cell(int Column, int Row);
}
