using Geeratya.Jira.Comments.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// Lays the loaded comments out as threads (<c>docs/internal/ux-screens.md</c>, "Comments"): a top-level comment keeps
/// its place in the loaded order (<c>comments.order</c>), and the replies to it follow it, oldest first. A comment
/// whose parent is not loaded (a later page, deleted) or that names itself shows flat, as a top-level comment. Pure.
/// </summary>
public static class CommentThreads
{
    /// <summary>Deepest indent level; a reply deeper than this shows at this level.</summary>
    public const int MaxDepth = 3;

    /// <summary>The comments in display order, each with the indent level of its reply chain (0 for top-level).</summary>
    public static IReadOnlyList<ThreadedComment> Arrange(IReadOnlyList<IssueComment> comments)
    {
        if (comments.All(static comment => comment.ParentId is null))
        {
            return [.. comments.Select(static comment => new ThreadedComment(comment, 0))];
        }

        var ids = comments.Select(static comment => comment.Id).ToHashSet();
        var replies = RepliesByParent(comments, ids);
        var result = new List<ThreadedComment>(comments.Count);
        var seen = new HashSet<IssueComment>(ReferenceEqualityComparer.Instance);

        foreach (var comment in comments)
        {
            if (!IsReply(comment, ids))
            {
                Add(result, seen, replies, comment, 0);
            }
        }

        // A reply chain that loops back on itself has no top-level comment; it shows flat rather than vanishing.
        foreach (var comment in comments)
        {
            if (!seen.Contains(comment))
            {
                Add(result, seen, replies, comment, 0);
            }
        }

        return result;
    }

    private static Dictionary<string, List<IssueComment>> RepliesByParent(
        IReadOnlyList<IssueComment> comments,
        HashSet<string> ids)
    {
        var replies = new Dictionary<string, List<IssueComment>>();

        foreach (var comment in comments.Where(comment => IsReply(comment, ids)))
        {
            if (!replies.TryGetValue(comment.ParentId!, out var list))
            {
                list = [];
                replies[comment.ParentId!] = list;
            }

            list.Add(comment);
        }

        foreach (var list in replies.Values)
        {
            // A stable sort: replies without a time keep the loaded order.
            var ordered = list.OrderBy(static comment => comment.Created ?? DateTimeOffset.MinValue).ToList();
            list.Clear();
            list.AddRange(ordered);
        }

        return replies;
    }

    private static bool IsReply(IssueComment comment, HashSet<string> ids)
    {
        return comment.ParentId is { } parent && parent != comment.Id && ids.Contains(parent);
    }

    private static void Add(
        List<ThreadedComment> result,
        HashSet<IssueComment> seen,
        Dictionary<string, List<IssueComment>> replies,
        IssueComment comment,
        int depth)
    {
        if (!seen.Add(comment))
        {
            return;
        }

        result.Add(new ThreadedComment(comment, Math.Min(depth, MaxDepth)));

        if (replies.TryGetValue(comment.Id, out var children))
        {
            foreach (var child in children)
            {
                Add(result, seen, replies, child, depth + 1);
            }
        }
    }
}
