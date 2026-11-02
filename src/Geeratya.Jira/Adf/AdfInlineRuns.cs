using System.Text;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Helpers shared by the ADF writers: merging adjacent text nodes with the same marks, comparing mark sets and a
/// fixed mark order, so the JSON and the Markdown-like text of one document do not depend on how Jira split it.
/// </summary>
internal static class AdfInlineRuns
{
    private const int StrongRank = 0;
    private const int EmphasisRank = 1;
    private const int StrikeRank = 2;
    private const int UnderlineRank = 3;
    private const int CodeRank = 4;
    private const int LinkRank = 5;
    private const int OtherRank = 6;

    public const int RankCount = OtherRank + 1;

    /// <summary>Drops empty text nodes and joins neighbours that carry the same set of marks.</summary>
    public static List<AdfNode> Merge(IReadOnlyList<AdfNode> nodes)
    {
        var merged = new List<AdfNode>(nodes.Count);
        StringBuilder? pending = null;
        AdfText? first = null;

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];

            if (node is AdfText text)
            {
                if (text.Text.Length == 0)
                {
                    continue;
                }

                if (first is not null && AreSameMarks(first.Marks, text.Marks))
                {
                    pending ??= new StringBuilder(first.Text);
                    pending.Append(text.Text);

                    continue;
                }

                Flush(merged, first, pending);
                pending = null;
                first = text;

                continue;
            }

            Flush(merged, first, pending);
            pending = null;
            first = null;
            merged.Add(node);
        }

        Flush(merged, first, pending);

        return merged;
    }

    public static bool AreSameMarks(IReadOnlyList<AdfMark> left, IReadOnlyList<AdfMark> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!Contains(right, left[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static int Rank(AdfMark mark)
    {
        return mark switch
        {
            AdfStrongMark => StrongRank,
            AdfEmphasisMark => EmphasisRank,
            AdfStrikeMark => StrikeRank,
            AdfUnderlineMark => UnderlineRank,
            AdfCodeMark => CodeRank,
            AdfLinkMark => LinkRank,
            _ => OtherRank,
        };
    }

    private static bool Contains(IReadOnlyList<AdfMark> marks, AdfMark mark)
    {
        for (var index = 0; index < marks.Count; index++)
        {
            if (marks[index].Equals(mark))
            {
                return true;
            }
        }

        return false;
    }

    private static void Flush(List<AdfNode> merged, AdfText? first, StringBuilder? pending)
    {
        if (first is null)
        {
            return;
        }

        merged.Add(pending is null ? first : first with { Text = pending.ToString() });
    }
}
