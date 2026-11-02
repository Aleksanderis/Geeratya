using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Jump;

/// <summary>
/// The rows of the jump prompt: the typed key first, then keys of the active tab (opened issues, then its rows) and
/// recently opened keys, filtered by what is typed. Pure.
/// </summary>
public static class JumpRows
{
    /// <summary>The most rows offered.</summary>
    public const int MaxRows = 30;

    /// <summary>Recently opened keys remembered per site session.</summary>
    public const int MaxRecent = 20;

    public static IReadOnlyList<JumpRow> For(AppState state, JumpState jump)
    {
        var parse = JumpKey.Parse(jump.Text, DefaultProject(state));
        var needle = jump.Text.Trim().ToUpperInvariant();
        var candidates = Candidates(state);
        var rows = new List<JumpRow>();
        var seen = new HashSet<string>();

        if (parse.Kind == JumpParseKind.Key)
        {
            var known = candidates.FirstOrDefault(candidate => candidate.Key == parse.Key);

            rows.Add(new JumpRow(parse.Key, known?.Title ?? string.Empty, JumpOrigin.Typed));
            seen.Add(parse.Key);
        }

        foreach (var candidate in candidates)
        {
            if (rows.Count >= MaxRows)
            {
                break;
            }

            if (candidate.Key.Contains(needle, StringComparison.Ordinal) && seen.Add(candidate.Key))
            {
                rows.Add(candidate);
            }
        }

        return rows;
    }

    /// <summary>
    /// The project bare numbers are completed with: that of the issue shown, else of the selected row, else of the
    /// last opened key; null when the session has none.
    /// </summary>
    public static string? DefaultProject(AppState state)
    {
        var tab = ActiveTab(state);

        if (tab is { Detail.Count: > 0 })
        {
            return JumpKey.ProjectOf(tab.Detail[^1].Key);
        }

        if (tab is not null && tab.Selected >= 0 && tab.Selected < tab.Rows.Count)
        {
            return tab.Rows[tab.Selected].ProjectKey;
        }

        return state.Session.RecentKeys.Count > 0 ? JumpKey.ProjectOf(state.Session.RecentKeys[0]) : null;
    }

    private static List<JumpRow> Candidates(AppState state)
    {
        var rows = new List<JumpRow>();

        if (ActiveTab(state) is { } tab)
        {
            foreach (var detail in tab.Detail.Reverse())
            {
                var title = detail.Row?.Summary ?? detail.Issue?.Summary ?? string.Empty;

                rows.Add(new JumpRow(detail.Key, title, JumpOrigin.Tab));
            }

            rows.AddRange(tab.Rows.Select(row => new JumpRow(row.Key, row.Summary, JumpOrigin.Tab)));
        }

        rows.AddRange(state.Session.RecentKeys.Select(key => new JumpRow(key, string.Empty, JumpOrigin.Recent)));

        return rows;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }
}
