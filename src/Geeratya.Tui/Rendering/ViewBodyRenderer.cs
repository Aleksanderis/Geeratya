using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A plain body for the active tab: title with the row count, the load state, then the rows from the scroll
/// position. Temporary: the list layout (#79) replaces it with columns, a selection highlight and wide-text care.
/// </summary>
public sealed class ViewBodyRenderer : IBodyRenderer
{
    private const int HeaderRows = 2;

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is not null;
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var lines = new List<StyledLine>
        {
            StyledLine.Of($"{tab.Definition.Title}  ({tab.Rows.Count} issues)", TextRole.Heading),
            StateLine(tab),
        };

        var first = Math.Clamp(tab.Scroll, 0, Math.Max(0, tab.Rows.Count - 1));
        var limit = Math.Max(HeaderRows, height);

        for (var index = first; index < tab.Rows.Count && lines.Count < limit; index++)
        {
            var selected = index == tab.Selected;
            var row = tab.Rows[index];

            lines.Add(StyledLine.Of($"{(selected ? "> " : "  ")}{row.Key}  {row.Summary}", selected ? TextRole.Selected : TextRole.Body));
        }

        return lines;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;
    }

    private static StyledLine StateLine(TabState tab)
    {
        return tab.Load switch
        {
            Loading { Reason: LoadReason.NextPage } => StyledLine.Of("Loading more...", TextRole.Muted),
            Loading => StyledLine.Of("Loading...", TextRole.Muted),
            Failed failed => StyledLine.Of(failed.Message, TextRole.Error),
            _ when tab.Truncated => StyledLine.Of($"Load more stopped at {ViewPaging.MaxRowsPerTab:N0} rows.", TextRole.Muted),
            _ when tab.Rows.Count == 0 => StyledLine.Of("No issues.", TextRole.Muted),
            _ => StyledLine.Empty,
        };
    }
}
