using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The which-key popup: while a multi-key binding is half typed in Normal mode, a box at the bottom of the body
/// lists the keys that continue it. Keys and labels come from the keymap and <see cref="ActionCatalog"/>, never from
/// text of its own. Rows beyond the box height are cut. Pure. Switched off by <c>:set whichkey off</c>.
/// </summary>
public static class WhichKeyOverlay
{
    private const int MaxBoxWidth = 50;
    private const int MinimumBoxWidth = 16;
    private const int MinimumBoxHeight = 3;
    private const int KeyColumn = 10;
    private const string GroupMarker = "+";

    /// <summary>True when the popup shows for <paramref name="state"/>: Normal mode, keys pending, switch on.</summary>
    public static bool Applies(AppState state)
    {
        return state is { Mode: InputMode.Normal, Settings.WhichKey: true } && state.PendingKeys.Length > 0;
    }

    /// <summary>The text rows of the popup (<c>key  label</c>), one per next key; empty when nothing continues.</summary>
    public static IReadOnlyList<string> Rows(string pending, KeymapTable keymap)
    {
        return keymap.NextKeys(InputMode.Normal, pending)
            .Select(next => $"{next.Key.PadRight(KeyColumn)}{Label(next, pending, keymap)}")
            .ToList();
    }

    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var rows = Rows(state.PendingKeys, keymap);
        var boxWidth = Math.Min(width, MaxBoxWidth);

        if (rows.Count == 0 || boxWidth < MinimumBoxWidth || height < MinimumBoxHeight)
        {
            return body;
        }

        var shown = rows.Take(height - 2).ToList();
        var contentWidth = boxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, state.PendingKeys) };

        lines.AddRange(shown.Select(row => OverlayBox.Wall(StyledLine.Of(row), contentWidth)));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var result = body.ToList();
        var top = result.Count - lines.Count;

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Place(lines[index], 0, width);
        }

        return result;
    }

    private static string Label(NextKey next, string pending, KeymapTable keymap)
    {
        if (next.Action is not null)
        {
            return ActionCatalog.FindTitle(next.Action) ?? next.Action;
        }

        return $"{GroupMarker}{GroupName(keymap, $"{pending} {next.Key}")} ({next.Count})";
    }

    /// <summary>
    /// The namespace of the first action below <paramref name="pending"/> (<c>issue</c> of <c>issue.delete</c>).
    /// </summary>
    private static string GroupName(KeymapTable keymap, string pending)
    {
        var next = keymap.NextKeys(InputMode.Normal, pending);

        while (next.Count > 0 && next[0].Action is null)
        {
            pending = $"{pending} {next[0].Key}";
            next = keymap.NextKeys(InputMode.Normal, pending);
        }

        var action = next.Count > 0 ? next[0].Action! : "keys";
        var dot = action.IndexOf('.');

        return dot < 0 ? action : action[..dot];
    }
}
