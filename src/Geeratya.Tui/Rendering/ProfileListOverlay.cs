using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The profile list box (<c>:profile list</c>): a centred box over the body with one row per saved profile (name,
/// account, site, the active one marked), the Add and Edit buttons and a key footer. Enter or a click switches,
/// <c>Ctrl-a</c> adds, <c>Ctrl-e</c> edits, <c>Ctrl-x</c> or a right-click asks to remove. Pure.
/// </summary>
public static class ProfileListOverlay
{
    private const string Title = "Profiles";
    private const string ActiveText = "active";
    private const int MaxBoxWidth = 100;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int FooterHeight = 3;
    private const int FooterMinimumInner = 8;
    private const string AddLabel = "Add";
    private const string EditLabel = "Edit";
    private const int ButtonGap = 3;
    private const int NameColumn = 16;
    private const int TopGap = 2;

    /// <summary>Draws the list of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (state.ProfileList is not { } list || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var profiles = state.Profiles.Profiles;
        var selected = Math.Clamp(list.Selected, 0, Math.Max(0, profiles.Count - 1));
        var room = height - 2;
        var footer = room >= FooterMinimumInner ? FooterHeight : 0;
        var inner = Math.Min(room, profiles.Count + footer);
        var area = Math.Max(0, inner - footer);
        var first = FirstVisible(selected, profiles.Count, area);
        var box = BoxLines(state, selected, first, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, box.Count);

        var shown = Math.Max(0, Math.Min(area, profiles.Count - first));

        OverlayBox.Mark(regions, MouseTarget.ProfileRow, top, left, boxWidth, 1, Enumerable.Range(first, shown));

        if (footer > 0)
        {
            MarkButtons(regions, top + area + 2, left);
        }

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], box[index], left, width);
        }

        return result;
    }

    private static int FirstVisible(int selected, int count, int area)
    {
        return area <= 0 || selected < area ? 0 : Math.Min(selected - area + 1, Math.Max(0, count - area));
    }

    private static List<StyledLine> BoxLines(
        AppState state,
        int selected,
        int first,
        KeymapTable keymap,
        int boxWidth,
        int inner,
        int footer)
    {
        var contentWidth = boxWidth - 2;
        var area = Math.Max(0, inner - footer);
        var profiles = state.Profiles.Profiles;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, Title) };

        for (var index = first; index < profiles.Count && lines.Count <= area; index++)
        {
            var row = RowLine(profiles[index], state.Profiles.ActiveName, index == selected, boxWidth - 4);

            lines.Add(OverlayBox.Wall(row, contentWidth));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(ButtonLine(), contentWidth));
            lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(keymap), TextRole.Muted), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    private static StyledLine RowLine(Profile profile, string? activeName, bool selected, int width)
    {
        var marker = selected ? ">" : " ";
        var active = profile.Name == activeName ? $" ({ActiveText})" : string.Empty;
        var name = TextWidth.Pad(profile.Name, NameColumn);
        var detail = $"{profile.Email} on {profile.SiteUrl}{active}";

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{marker} {name}{detail}", width), TextRole.Selected);
        }

        return new StyledLine(
        [
            new StyledSpan(marker, TextRole.Muted),
            new StyledSpan($" {name}", TextRole.Strong),
            new StyledSpan(detail, TextRole.Muted),
        ]);
    }

    private static StyledLine ButtonLine()
    {
        var gap = new string(' ', ButtonGap);

        return new StyledLine(
        [
            new StyledSpan(AddLabel, TextRole.Key),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(EditLabel, TextRole.Key),
        ]);
    }

    private static void MarkButtons(List<OverlayRegion>? regions, int row, int left)
    {
        if (regions is null)
        {
            return;
        }

        var column = left + 2;

        regions.Add(new OverlayRegion(MouseTarget.ProfileAddButton, row, column, AddLabel.Length, 1, 0));
        regions.Add(
            new OverlayRegion(
                MouseTarget.ProfileEditButton,
                row,
                column + AddLabel.Length + ButtonGap,
                EditLabel.Length,
                1,
                0));
    }

    private static string FooterText(KeymapTable keymap)
    {
        var accept = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var add = keymap.FindKeys(InputMode.Insert, ActionIds.ProfileAdd);
        var edit = keymap.FindKeys(InputMode.Insert, ActionIds.ProfileEdit);
        var remove = keymap.FindKeys(InputMode.Insert, ActionIds.ProfileRemove);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        return $"{accept} switch  {add} add  {edit} edit  {remove} remove  {cancel} close";
    }
}
