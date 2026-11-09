using Geeratya.Core.Detail;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Clipboard;

/// <summary>What of an issue is copied (#876).</summary>
public enum IssueYankForm
{
    /// <summary>The issue key, <c>PROJ-123</c>.</summary>
    Key,

    /// <summary>The address of the issue page, <c>https://site/browse/PROJ-123</c>.</summary>
    Link,

    /// <summary>The key and the summary, <c>PROJ-123 Fix the login</c>.</summary>
    Summary,
}

/// <summary>
/// Copies an issue to the clipboard (#876): the focused issue of a board, list or backlog, the shown issue of the
/// detail, or the sub-task, linked issue, parent or epic the detail cursor rests on. The copy itself is an effect. Pure.
/// </summary>
public static class IssueYank
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Copies the focused issue in the given form; warns when there is none.</summary>
    public static ReduceResult Copy(AppState state, IssueYankForm form)
    {
        if (HoldsSet(state))
        {
            return Warn(state, "Copy takes one issue: clear the selection first");
        }

        if (Focused(state) is not { } issue)
        {
            return Warn(state, "No issue selected: select a row or open an issue to copy it");
        }

        var text = issue.Key;
        var what = issue.Key;

        if (form == IssueYankForm.Link)
        {
            if (LinkOf(state, issue.Key) is not { } link)
            {
                return Warn(state, "Cannot copy the link: no active site");
            }

            text = link;
            what = $"the link of {issue.Key}";
        }
        else if (form == IssueYankForm.Summary && issue.Summary is { Length: > 0 } summary)
        {
            text = $"{issue.Key} {summary}";
            what = $"{issue.Key} and its summary";
        }

        return new ReduceResult(state, [new CopyToClipboardEffect(text, what)]);
    }

    private static bool HoldsSet(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Detail.Count: 0 } && RowSelection.HoldsASet(tab);
    }

    private static string? LinkOf(AppState state, string key)
    {
        if (state.Profiles.Active is not { } profile || !Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        return IssueUrls.Browse(new JiraConnection(site, profile.Email), key)?.AbsoluteUri;
    }

    private static Focus? Focused(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return null;
        }

        return tab.Detail.Count > 0 ? InDetail(tab.Detail[^1]) : InView(tab);
    }

    private static Focus? InView(TabState tab)
    {
        if (tab.FocusedSprintId is not null)
        {
            return null;
        }

        var row = tab.Definition.Layout == ViewLayout.Columns
            ? ColumnsNavigation.SelectedIssue(tab)
            : tab.Rows.ElementAtOrDefault(tab.Selected);

        return row is null ? null : new Focus(row.Key, row.Summary);
    }

    private static Focus InDetail(DetailState detail)
    {
        var shown = new Focus(detail.Key, detail.Issue?.Summary ?? detail.Row?.Summary);

        return DetailReducer.TargetOf(detail) switch
        {
            { Kind: DetailTargetKind.Issue } target => Related(detail.Issue, target.Value),
            { Kind: DetailTargetKind.Field, Value: DetailFieldIds.Parent or DetailFieldIds.Epic }
                when (detail.Issue?.Epic ?? detail.Issue?.Parent) is { } parent => new Focus(parent.Key, parent.Summary),
            _ => shown,
        };
    }

    private static Focus Related(IssueDetail? issue, string key)
    {
        var stub = issue?.Subtasks.Concat(issue.Links.Select(static link => link.Issue)).FirstOrDefault(s => s.Key == key);

        return new Focus(key, stub?.Summary);
    }

    private static ReduceResult Warn(AppState state, string message)
    {
        return new ReduceResult(StatusMessages.Warn(state, message), _noEffects);
    }

    private sealed record Focus(string Key, string? Summary);
}
