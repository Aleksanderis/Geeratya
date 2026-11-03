using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.State;

/// <summary>The Confirm dialog: how a request is split, wrapped and sized, and whether it fits. Pure.</summary>
public static class ConfirmDialog
{
    public const string Title = "Confirm";
    public const string CancelLabel = "Cancel";

    /// <summary>The words the status bar shows while the dialog is open (the keys follow from the keymap).</summary>
    public const string StatusText = "Confirm";

    /// <summary>The widest box; wider terminals get more margin, not longer lines.</summary>
    public const int MaxBoxWidth = 64;

    /// <summary>The narrowest box that is still readable.</summary>
    public const int MinBoxWidth = 24;

    private const int Margin = 4;
    private const int NoticeChrome = 4;
    private const string FallbackLabel = "Confirm";

    /// <summary>
    /// The layout for <paramref name="request"/> in a body of <paramref name="columns"/> by <paramref name="bodyRows"/>
    /// cells, or null when the box does not fit: an answer nobody can read is no confirmation.
    /// </summary>
    public static ConfirmDialogLayout? Layout(ConfirmRequest request, int columns, int bodyRows)
    {
        var boxWidth = Math.Min(columns - Margin, MaxBoxWidth);

        if (boxWidth < MinBoxWidth)
        {
            return null;
        }

        var text = boxWidth - 4;

        if (request.Title is not null)
        {
            var rows = NoticeRows(request.Message, text, bodyRows);
            var notice = new ConfirmDialogLayout(boxWidth, rows, [], LabelOf(request), request.Title, true);

            return notice.Height <= bodyRows ? notice : null;
        }

        var split = Split(request.Message);
        var question = Wrap(split.Question, text);
        var layout = new ConfirmDialogLayout(boxWidth, question, Wrap(split.Warning, text), LabelOf(request));

        return layout.Height <= bodyRows ? layout : null;
    }

    /// <summary>
    /// The wrapped lines of a notice, paragraphs kept; cut with an ellipsis on the last line when the body is too short
    /// to show them all, so a long text is still readable in part.
    /// </summary>
    private static List<string> NoticeRows(string message, int text, int bodyRows)
    {
        var rows = new List<string>();

        foreach (var paragraph in message.Split('\n'))
        {
            rows.AddRange(paragraph.Trim().Length == 0 ? [string.Empty] : Wrap(paragraph, text));
        }

        var room = Math.Max(1, bodyRows - NoticeChrome);

        if (rows.Count <= room)
        {
            return rows;
        }

        rows = [.. rows.Take(room)];
        rows[^1] = $"{Wrap(rows[^1], Math.Max(1, text - 1)).FirstOrDefault() ?? string.Empty}\u2026";

        return rows;
    }

    /// <summary>
    /// True when the dialog for the open request fits the last size the host reported; an unknown size (nothing
    /// reported yet) counts as fitting, because the status line then still shows the question (see the status bar).
    /// </summary>
    public static bool Fits(AppState state)
    {
        if (state.Confirm is not { } request || state.ViewportColumns <= 0
            || state.ViewportBodyHeight == AppState.UnknownBodyHeight)
        {
            return true;
        }

        return Layout(request, state.ViewportColumns, state.ViewportBodyHeight) is not null;
    }

    /// <summary>The name of the <c>y</c> answer: the request's own label, else a plain "Confirm".</summary>
    public static string LabelOf(ConfirmRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Label) ? FallbackLabel : request.Label;
    }

    /// <summary>
    /// Splits a question into what happens and the warning: the sentences that say nothing can be undone go to the
    /// warning, in their order; the rest stays the question. Quoted text (a comment's words) is never judged, and
    /// the wording is not changed.
    /// </summary>
    public static ConfirmSplit Split(string message)
    {
        var question = new List<string>();
        var warning = new List<string>();

        foreach (var sentence in Sentences(message))
        {
            (IsWarning(sentence) ? warning : question).Add(sentence);
        }

        return new ConfirmSplit(string.Join(' ', question), string.Join(' ', warning));
    }

    /// <summary>
    /// Wraps <paramref name="text"/> at spaces to <paramref name="width"/> columns; a word wider than a line is cut
    /// between text elements, so a wide or combining character is never split. Empty text gives no lines.
    /// </summary>
    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        var used = 0;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var length = DisplayWidth.Of(word);

            if (used > 0 && used + 1 + length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
                used = 0;
            }

            if (used > 0)
            {
                line.Append(' ');
                used++;
            }

            if (length <= width)
            {
                line.Append(word);
                used += length;
                continue;
            }

            var elements = StringInfo.GetTextElementEnumerator(word);

            while (elements.MoveNext())
            {
                var element = (string)elements.Current;
                var size = DisplayWidth.Of(element);

                if (used + size > width && used > 0)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    used = 0;
                }

                line.Append(element);
                used += size;
            }
        }

        if (used > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    private static bool IsWarning(string sentence)
    {
        var outside = new StringBuilder();
        var quoted = false;

        foreach (var character in sentence)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted)
            {
                outside.Append(character);
            }
        }

        var text = outside.ToString();

        return text.Contains("undo", StringComparison.OrdinalIgnoreCase);
    }

    // A sentence ends at ".", "?" or "!" followed by a space or the end, outside double quotes; the mark stays with it.
    private static IEnumerable<string> Sentences(string message)
    {
        var start = 0;
        var quoted = false;

        for (var index = 0; index < message.Length; index++)
        {
            if (message[index] == '"')
            {
                quoted = !quoted;
            }

            var ends = !quoted && message[index] is '.' or '?' or '!'
                && (index + 1 == message.Length || message[index + 1] == ' ');

            if (!ends)
            {
                continue;
            }

            var sentence = message[start..(index + 1)].Trim();

            if (sentence.Length > 0)
            {
                yield return sentence;
            }

            start = index + 1;
        }

        var rest = message[start..].Trim();

        if (rest.Length > 0)
        {
            yield return rest;
        }
    }
}
