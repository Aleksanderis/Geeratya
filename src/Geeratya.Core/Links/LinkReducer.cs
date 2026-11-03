using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.Links;

/// <summary>
/// <c>issue.unlink</c>: the link under the cursor of the issue detail is removed after a confirm. Nothing is written
/// here; the confirm carries the write, which runs on <c>y</c> and is checked again then (a busy issue is refused at
/// that moment). Pure: the time of the key comes in as an argument.
/// </summary>
public static class LinkReducer
{
    private const string NoIssueText = "Open an issue and put the cursor on a linked issue first.";
    private const string NoLinkText = "The cursor is not on a linked issue.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Unlink(AppState state, DateTimeOffset at)
    {
        var session = state.Session;
        var tab = session.Active is { } id ? ViewReducer.Find(session.Tabs, id) : null;

        if (tab is not { Detail.Count: > 0 })
        {
            return Say(state, NoIssueText);
        }

        var shown = tab.Detail[^1];
        var target = DetailReducer.TargetOf(shown);
        var link = shown.Issue?.Links.FirstOrDefault(candidate => candidate.Id == target?.LinkId);

        if (link is null)
        {
            return Say(state, NoLinkText);
        }

        var request = new UnlinkIssuesRequest(shown.Key, link.Issue.Key, link.Id, link.Direction, link.DirectionText);
        var confirm = new ConfirmRequest(
            request.ConfirmText,
            ActionIds.IssueUnlink,
            shown.Key,
            new WriteRequested(tab.Id, request, at),
            "Remove link");

        return new ReduceResult(
            state with { Mode = InputMode.Confirm, Confirm = confirm, PendingKeys = string.Empty, Message = null },
            _noEffects);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
