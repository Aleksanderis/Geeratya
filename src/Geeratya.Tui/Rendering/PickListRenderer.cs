using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The one renderer of every pick list (<c>docs/internal/ui-consistency.md</c>, component 5): the highlighted row, the
/// window of rows that fits with its scroll markers, the notice for a slow, failed or empty list, and the mouse
/// regions of the rows. The pick-list overlays (picker, Jump, palette, context menu, Settings, profile list) and the
/// popups of the form, create pane, dropdown and comment editor all draw their rows through it, so they look and
/// click the same. Pure.
/// </summary>
public static class PickListRenderer
{
    /// <summary>Rows a popup under a field shows at most.</summary>
    public const int PopupRows = 5;

    private const string SearchingText = "searching…";
    private const string LookupFailedText = "lookup failed, showing what is loaded";
    private const string NoMatchText = "No match";
    private const string NoMoreText = "No more values";

    /// <summary>
    /// A row of text after <paramref name="margin"/>: two columns of pointer space, then the text. The highlighted
    /// row takes the <see cref="TextRole.Selected"/> role.
    /// </summary>
    public static StyledLine Row(string text, bool highlighted, string margin = "")
    {
        return StyledLine.Of($"{margin}  {text}", highlighted ? TextRole.Selected : TextRole.Body);
    }

    /// <summary>
    /// What to say under the rows: searching while the lookup is slow, why it failed, or that nothing matches (other
    /// words when no filter is typed); null when the rows speak for themselves.
    /// </summary>
    public static PickNotice? Notice(PickList pick, int count, bool queryEmpty)
    {
        return pick.Status switch
        {
            PickStatus.Loading => new PickNotice(SearchingText, TextRole.Loading),
            PickStatus.Failed => new PickNotice($"{LookupFailedText}: {pick.Message}", TextRole.Error),
            PickStatus.Ready when count == 0 && queryEmpty => new PickNotice(NoMoreText, TextRole.Muted),
            PickStatus.Ready when count == 0 => new PickNotice(NoMatchText, TextRole.Muted),
            _ => null,
        };
    }

    /// <summary>
    /// The rows of a field popup: at most <see cref="PopupRows"/> suggestions around the highlight, then the notice
    /// line when there is one. Each row stands for its suggestion index, the notice for none.
    /// <paramref name="emptyListsAll"/> says whether an empty filter means "every value" (a field popup, whose empty
    /// list reads "No more values") or not (the comment popup, where it reads "No match").
    /// </summary>
    public static List<PickRow> Popup(SuggestionState popup, int width, string margin = "", bool emptyListsAll = true)
    {
        var window = popup.Pick.Window(popup.Items.Count, PopupRows);
        var highlighted = popup.Pick.Index(popup.Items.Count);
        var rows = new List<PickRow>();

        foreach (var index in window.Indexes)
        {
            var text = DisplayText.Cut(SuggestionText.Row(popup.Items[index]), width);

            rows.Add(new PickRow(Row(text, index == highlighted, margin), index));
        }

        if (Notice(popup.Pick, popup.Items.Count, emptyListsAll && popup.Query.Length == 0) is { } notice)
        {
            var text = DisplayText.Cut(notice.Text, width);

            rows.Add(new PickRow(StyledLine.Of($"{margin}  {text}", notice.Role), -1));
        }

        return rows;
    }

    /// <summary>
    /// The item index each row of <see cref="Popup"/> stands for (-1 for the notice row), for the mouse regions of a
    /// popup whose rows are drawn elsewhere.
    /// </summary>
    public static List<int> PopupItems(SuggestionState popup)
    {
        var items = new List<int>(popup.Pick.Window(popup.Items.Count, PopupRows).Indexes);

        if (Notice(popup.Pick, popup.Items.Count, popup.Query.Length == 0) is not null)
        {
            items.Add(-1);
        }

        return items;
    }

    /// <summary>
    /// The box rows of <paramref name="lines"/> inside <paramref name="window"/>, each closed by a wall whose right
    /// column carries the scroll marker of a list of <paramref name="area"/> visible rows.
    /// </summary>
    public static List<StyledLine> Walls(IReadOnlyList<StyledLine> lines, PickWindow window, int area, int contentWidth)
    {
        var walls = new List<StyledLine>();

        for (var row = 0; row < window.Shown; row++)
        {
            var marker = OverlayBox.ScrollMarker(row, area, window.First, lines.Count);

            walls.Add(OverlayBox.Wall(lines[window.First + row], contentWidth, marker));
        }

        return walls;
    }

    /// <summary>
    /// Notes the clickable rows inside <paramref name="window"/> in <paramref name="regions"/> (when not null):
    /// display line n of the window stands for <paramref name="itemOfLine"/>[First + n] (a negative one is not
    /// clickable), starting at box row <paramref name="offset"/>.
    /// </summary>
    public static void Mark(
        List<OverlayRegion>? regions,
        MouseTarget target,
        BoxPlace place,
        int offset,
        PickWindow window,
        IReadOnlyList<int> itemOfLine)
    {
        OverlayBox.Mark(
            regions,
            target,
            place.Top,
            place.Left,
            place.Width,
            offset,
            itemOfLine.Skip(window.First).Take(window.Shown));
    }

    /// <summary>As the other overload, for a list whose display lines are its rows.</summary>
    public static void Mark(
        List<OverlayRegion>? regions,
        MouseTarget target,
        BoxPlace place,
        int offset,
        PickWindow window)
    {
        OverlayBox.Mark(regions, target, place.Top, place.Left, place.Width, offset, window.Indexes);
    }
}
