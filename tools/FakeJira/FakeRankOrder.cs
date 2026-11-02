using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The order the rank calls gave: remembered as moves and laid over the issue lists of the board and backlog replies,
/// so a reload after a rank shows the new order. A list is reordered by the moves whose issues are both in it, so
/// the order of a board that is served in several pages holds within a page only.
/// </summary>
public sealed class FakeRankOrder
{
    private const string IssuesName = "issues";
    private const string KeyName = "key";
    private const string BeforeName = "rankBeforeIssue";
    private const string AfterName = "rankAfterIssue";

    private readonly Lock _lock = new();
    private readonly List<Move> _moves = [];

    /// <summary>Remembers the move of a rank call body; a body that names no issue or reference is ignored.</summary>
    public void Record(string body)
    {
        try
        {
            if (JsonNode.Parse(body) is not JsonObject node)
            {
                return;
            }

            var before = node[BeforeName]?.GetValue<string>();
            var reference = before ?? node[AfterName]?.GetValue<string>();

            if (reference is null || node[IssuesName] is not JsonArray issues)
            {
                return;
            }

            lock (_lock)
            {
                foreach (var issue in issues.Select(item => item?.GetValue<string>()).OfType<string>())
                {
                    _moves.Add(new Move(issue, reference, before is not null));
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            // A body the store could not read is answered by the store; there is nothing to remember.
        }
    }

    /// <summary>
    /// The reply with its <c>issues</c> reordered by the remembered moves; the reply itself when none apply.
    /// </summary>
    public FakeReply Apply(FakeReply reply)
    {
        Move[] moves;

        lock (_lock)
        {
            moves = [.. _moves];
        }

        if (moves.Length == 0 || reply.Status != 200)
        {
            return reply;
        }

        try
        {
            if (JsonNode.Parse(reply.Body) is not JsonObject node || node[IssuesName] is not JsonArray issues)
            {
                return reply;
            }

            var list = issues.Select(issue => issue!.DeepClone()).ToList();

            foreach (var move in moves)
            {
                Place(list, move);
            }

            issues.Clear();

            foreach (var issue in list)
            {
                issues.Add(issue);
            }

            return reply with { Body = Encoding.UTF8.GetBytes(node.ToJsonString()) };
        }
        catch (JsonException)
        {
            return reply;
        }
    }

    private static void Place(List<JsonNode> list, Move move)
    {
        var from = list.FindIndex(issue => KeyOf(issue) == move.Key);

        if (from < 0 || list.FindIndex(issue => KeyOf(issue) == move.Reference) < 0)
        {
            return;
        }

        var moved = list[from];

        list.RemoveAt(from);

        var at = list.FindIndex(issue => KeyOf(issue) == move.Reference);

        list.Insert(move.Before ? at : at + 1, moved);
    }

    private static string? KeyOf(JsonNode issue)
    {
        return issue[KeyName]?.GetValue<string>();
    }

    private sealed record Move(string Key, string Reference, bool Before);
}
