namespace Geeratya.Jira.Adf;

/// <summary>Converts between ADF marks and <see cref="AdfMarkdownStyle" /> flags.</summary>
public static class AdfMarkdownStyleMarks
{
    public static AdfMarkdownStyle StyleOf(IReadOnlyList<AdfMark> marks)
    {
        var style = AdfMarkdownStyle.None;

        for (var index = 0; index < marks.Count; index++)
        {
            style |= marks[index] switch
            {
                AdfStrongMark => AdfMarkdownStyle.Strong,
                AdfEmphasisMark => AdfMarkdownStyle.Emphasis,
                AdfStrikeMark => AdfMarkdownStyle.Strike,
                AdfUnderlineMark => AdfMarkdownStyle.Underline,
                _ => AdfMarkdownStyle.None,
            };
        }

        return style;
    }

    /// <summary>The marks of a style, plus code and a link when given.</summary>
    public static List<AdfMark> MarksOf(AdfMarkdownStyle style, bool isCode, string? href)
    {
        var marks = new List<AdfMark>();

        if ((style & AdfMarkdownStyle.Strong) != 0)
        {
            marks.Add(new AdfStrongMark());
        }

        if ((style & AdfMarkdownStyle.Emphasis) != 0)
        {
            marks.Add(new AdfEmphasisMark());
        }

        if ((style & AdfMarkdownStyle.Strike) != 0)
        {
            marks.Add(new AdfStrikeMark());
        }

        if ((style & AdfMarkdownStyle.Underline) != 0)
        {
            marks.Add(new AdfUnderlineMark());
        }

        if (isCode)
        {
            marks.Add(new AdfCodeMark());
        }

        if (href is not null)
        {
            marks.Add(new AdfLinkMark(href));
        }

        return marks;
    }
}
