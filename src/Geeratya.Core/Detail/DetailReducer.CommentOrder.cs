using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    private const string OpenFirstText = "Open an issue first.";

    /// <summary>
    /// <c>comment.order</c> (<c>&lt;space&gt;mo</c>, #875): flips the comments of the shown issue between oldest first
    /// and newest first and saves the choice as the <c>comments.order</c> setting, which later issues start with.
    /// A list that holds every comment is reversed in place; otherwise the pages are read again in the new order,
    /// because paging is by offset in Jira's order. The other opened issues keep the order they were loaded in.
    /// </summary>
    public static ReduceResult ReverseComments(AppState state)
    {
        var tab = ActiveTab(state);

        if (tab is not { Detail.Count: > 0 })
        {
            return new ReduceResult(state with { Message = OpenFirstText }, []);
        }

        var top = tab.Detail[^1];
        var newest = !top.CommentsNewestFirst;
        var order = SettingsRegistry.Find(SettingsRegistry.CommentsOrderName)!;
        var saved = SettingsReducer.Change(state, order, newest ? "newest" : "oldest");
        var effects = saved.Effects.ToList();
        var flipped = top with { CommentsNewestFirst = newest, CommentsOffset = 0 };
        var complete = top.NextComments is null
            && top.CommentsLoad is IdleState
            && (top.CommentTotal is not { } total || total <= top.Comments.Count);
        var next = saved.State;

        if (complete)
        {
            flipped = flipped with { Comments = [.. top.Comments.Reverse()] };
        }
        else
        {
            var requestId = next.Sequence + 1;

            flipped = flipped with
            {
                Comments = [],
                NextComments = null,
                CommentsLoad = new Loading(LoadReason.Refresh),
                CommentsRequest = requestId,
            };
            effects.Add(new LoadIssueCommentsEffect(tab.Id, top.Key, 0, requestId, newest));
            next = next with { Sequence = requestId };
        }

        var laidOut = Layout(flipped, DetailSize.Columns(next), DetailSize.ListRows(next, tab));

        return new ReduceResult(ViewReducer.Replace(next, ReplaceTop(tab, laidOut)), effects);
    }
}
