using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The other half of the mouse wiring check (<c>MouseMapTests</c> checks the resolver): every clickable target of the
/// mouse table that an overlay draws must be reported by the frame of some real overlay, and each gesture the table
/// gives it must resolve to a command on that frame. A row whose target no renderer reports would sit in the table
/// and the document and never fire.
/// </summary>
public class MouseWiringTests
{
    private const int Width = 100;
    private const int Height = 30;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    // Targets that are no region of an overlay: the tab bar, the status line, the body and what covers it.
    private static readonly HashSet<MouseTarget> _notOverlayRegions =
    [
        MouseTarget.Tabs, MouseTarget.NewTabButton, MouseTarget.TabClose, MouseTarget.TabOverflow,
        MouseTarget.CommandsChip, MouseTarget.JumpChip, MouseTarget.LaneChip, MouseTarget.Banner,
        MouseTarget.Lists, MouseTarget.SubtaskRow, MouseTarget.RowMenuButton, MouseTarget.Detail,
        MouseTarget.StatusBadge, MouseTarget.Assignee, MouseTarget.NewIssueButton, MouseTarget.CommentsAdd,
        MouseTarget.Comment, MouseTarget.Status, MouseTarget.StatusMessage, MouseTarget.Backdrop,
        MouseTarget.HelpBody, MouseTarget.MessagesBody, MouseTarget.ConfirmBox, MouseTarget.OverlayBox,
    ];

    [Fact]
    public void GivenEveryWiredOverlayTarget_WhenDrawingTheOverlays_ThenSomeFrameReportsIt()
    {
        var reported = Frames().SelectMany(drawn => drawn.Frame.Regions.Select(region => region.Target)).ToHashSet();
        var wired = DefaultMouseMap.Rows
            .Where(row => MouseResolver.WiredParts.Contains(row.Part) && !_notOverlayRegions.Contains(row.Target))
            .Select(row => row.Target)
            .Distinct();

        var missing = wired.Where(target => !reported.Contains(target)).ToList();

        Assert.True(missing.Count == 0, $"Wired targets no overlay draws: {string.Join(", ", missing)}");
    }

    [Fact]
    public void GivenEveryWiredOverlayRow_WhenGesturingOnItsDrawnRegion_ThenItResolvesToACommand()
    {
        var silent = new List<string>();

        foreach (var drawn in Frames())
        {
            foreach (var region in drawn.Frame.Regions.Where(region => !_notOverlayRegions.Contains(region.Target)))
            {
                foreach (var row in DefaultMouseMap.Rows.Where(row => row.Target == region.Target
                    && MouseResolver.WiredParts.Contains(row.Part)
                    && row.Gesture is MouseGesture.Click or MouseGesture.RightClick))
                {
                    if (RegionProbe.Click(drawn.State, drawn.Frame, region, row.Gesture) is null)
                    {
                        silent.Add($"{row.Target} {row.Gesture}");
                    }
                }
            }
        }

        Assert.True(silent.Count == 0, $"Drawn rows that ignore their table gesture: {string.Join(", ", silent.Distinct())}");
    }

    private static List<DrawnOverlay> Frames()
    {
        List<AppState> states =
        [
            PaletteOverlayTests.Sample(string.Empty),
            JumpOverlaySnapshotTests.Sample(string.Empty),
            SourcePickerSnapshotTests.Sample(string.Empty),
            SettingsScreenTests.Sample(),
            SettingsScreenTests.WithThemeList(),
            ProfileManagerScreenTests.Sample(),
            ProfileFormOverlaySnapshotTests.Editing(),
            ContextMenuOverlayTests.Sample(),
            ConfirmOverlayTests.Open(),
            FormSuggestionOverlayTests.WithForm(FormSuggestionOverlayTests.LabelsForm(PickStatus.Ready)),
            CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.EditorWithPopup()),
            CreatePaneOverlaySnapshotTests.Open(CreatePaneOverlaySnapshotTests.Form()),
            HelpPageSnapshotTests.StateFor("list"),
        ];

        return [.. states.Select(state => new DrawnOverlay(state, FrameBuilder.BuildStyled(state, Width, Height, _keymap)!))];
    }

    private sealed record DrawnOverlay(AppState State, StyledFrame Frame);
}
