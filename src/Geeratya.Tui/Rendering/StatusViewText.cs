using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The view part of the status bar text (source and count on a board, breadcrumb and ruler in the detail) and how the
/// cursor position and the sprint goal share the room left after the hints. Pure.
/// </summary>
internal static class StatusViewText
{
    private const string Separator = " · ";
    private const int MinimumRoom = 8;

    /// <summary>
    /// What the screen no longer draws as a title line of its own: the source and its count on a board, the
    /// breadcrumb and the ruler in the issue detail (<c>docs/internal/ux-look.md</c>, sections 2 and 5).
    /// </summary>
    internal static string Of(AppState state, DateTimeOffset now, int room, bool withGoal)
    {
        if (ListBodyRenderer.ActiveTab(state) is not { } tab)
        {
            return string.Empty;
        }

        if (tab.Detail.Count > 0)
        {
            return CardText.Shorten(DetailStatus.Text(
                tab,
                room,
                DetailSize.ListRows(state, tab)), room);
        }

        if (tab.Definition.Layout != ViewLayout.Columns)
        {
            return string.Empty;
        }

        var glyphs = Glyphs.For(GlyphSet.Unicode);
        var full = ColumnsBodyRenderer.StatusText(tab, glyphs, now, withGoal);

        // The goal is the last text and shows only whole: when it does not fit it is left out, not cut.
        if (withGoal && TextWidth.Of(full) > room)
        {
            full = ColumnsBodyRenderer.StatusText(tab, glyphs, now, false);
        }

        return CardText.Shorten(full, room);
    }

    // The goal ranks above the cursor position: a position that would push the goal out stays hidden.
    internal static bool LosesGoal(AppState state, string shown, string positioned)
    {
        if (ListBodyRenderer.ActiveTab(state) is not { } tab)
        {
            return false;
        }

        var goal = ColumnsBodyRenderer.GoalText(tab);

        return goal.Length > 0 && shown.Contains($"{Separator}{goal}", StringComparison.Ordinal)
            && !positioned.Contains($"{Separator}{goal}", StringComparison.Ordinal);
    }

    /// <summary>The view text's room once the cursor position takes its cells: the board text gives way, not a hint.</summary>
    internal static int PositionedRoom(AppState state, int spare)
    {
        if (ListBodyRenderer.ActiveTab(state) is not { Detail.Count: 0 } tab)
        {
            return spare;
        }

        var position = SelectedIssueText.Text(tab);

        if (position.Length == 0)
        {
            return spare;
        }

        var reduced = spare - TextWidth.Of(position) - Separator.Length;

        return reduced >= MinimumRoom ? reduced : spare;
    }
}
