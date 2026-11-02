namespace Geeratya.Jira.Adf;

/// <summary>
/// Tells whether a document survives being edited as Markdown-like text: ADF to text to ADF gives the same document.
/// The UI warns before saving a description or comment for which this is false (tables, panels, expands, media,
/// decisions, colours, dates, statuses, unknown nodes, a document cut short by the parser).
/// </summary>
public static class AdfRoundTrip
{
    /// <summary>
    /// Compares the JSON the writer produces for both documents, so the order of marks, a split of one run of text
    /// into several nodes and the ids Jira gave task items do not count. Two things are treated as equal on purpose
    /// because the text keeps their meaning: empty paragraphs (blank lines carry no content) and an emoji that comes
    /// with its glyph (it becomes the plain character).
    /// </summary>
    public static bool RoundTrips(AdfDocument document)
    {
        if (document.IsTruncated)
        {
            return false;
        }

        var text = AdfMarkdownWriter.ToText(document);
        var parsed = AdfMarkdownParser.Parse(text);

        return AdfJsonWriter.ToJson(Normalize(document)) == AdfJsonWriter.ToJson(Normalize(parsed));
    }

    private static AdfDocument Normalize(AdfDocument document)
    {
        return document with { Content = NormalizeChildren(document.Content) };
    }

    private static List<AdfNode> NormalizeChildren(IReadOnlyList<AdfNode> children)
    {
        var normalized = new List<AdfNode>(children.Count);

        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];

            switch (child)
            {
                case AdfParagraph { Children.Count: 0 }:
                    break;
                case AdfEmoji { Text: { Length: > 0 } glyph }:
                    normalized.Add(new AdfText(glyph, []));

                    break;
                case AdfContainerNode container:
                    normalized.Add(container with { Children = NormalizeChildren(container.Children) });

                    break;
                default:
                    normalized.Add(child);

                    break;
            }
        }

        return normalized;
    }
}
