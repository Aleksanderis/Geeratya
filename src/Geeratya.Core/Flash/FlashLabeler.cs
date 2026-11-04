using System.Text;
using Geeratya.Core.Backlog;
using Geeratya.Core.Lists;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Core.Flash;

/// <summary>
/// Finds the rows in view that contain the typed text (in the key or the summary, as drawn) and gives each a one-letter
/// label, nearest to the selection first. Only the visible rows are looked at, so the cost does not depend on the
/// number of loaded rows. A letter that would continue some match in view is never a label, so typing more text and
/// pressing a label cannot be confused. Pure.
/// </summary>
public static class FlashLabeler
{
    /// <summary>Label letters, home row first.</summary>
    public const string Alphabet = "asdfghjklqwertyuiopzxcvbnm";

    internal const char Eligible = '\u0001';
    private const int KeySeparator = 1;

    /// <summary>The labels for <paramref name="text"/>, nearest to the selected row first. Empty text has none.</summary>
    /// <param name="tab">The list tab.</param>
    /// <param name="text">The typed text.</param>
    /// <param name="innerWidth">The width the rows are drawn in.</param>
    /// <param name="area">The number of rows in view.</param>
    public static IReadOnlyList<FlashLabel> Compute(TabState tab, string text, int innerWidth, int area)
    {
        if (text.Length == 0 || tab.Rows.Count == 0 || area <= 0)
        {
            return [];
        }

        var columns = ListViewColumns.For(tab, innerWidth, area);
        var first = Math.Clamp(tab.Scroll, 0, tab.Rows.Count - 1);
        var end = Math.Min(tab.Rows.Count, first + area);
        var rows = new List<int>(Math.Max(0, end - first));

        for (var row = first; row < end; row++)
        {
            rows.Add(row);
        }

        return ForRows(tab, text, columns, rows);
    }

    /// <summary>
    /// The labels for the rows of a Scrum backlog in view (section headers, notes and collapsed rows have none),
    /// drawn with the columns of <see cref="ListViewColumns.ForBacklog"/>.
    /// </summary>
    public static IReadOnlyList<FlashLabel> ComputeBacklog(TabState tab, string text, int innerWidth)
    {
        if (text.Length == 0 || tab.Rows.Count == 0 || tab.VisibleRows <= 0)
        {
            return [];
        }

        var layout = BacklogSections.Build(tab);
        var first = Math.Clamp(tab.Scroll, 0, Math.Max(0, layout.LineCount - 1));
        var end = Math.Min(layout.LineCount, first + tab.VisibleRows);
        var rows = new List<int>();

        for (var line = first; line < end; line++)
        {
            if (layout.LineAt(line) is { Row: { } row })
            {
                rows.Add(row);
            }
        }

        return ForRows(tab, text, ListViewColumns.ForBacklog(tab, innerWidth), rows);
    }

    private static List<FlashLabel> ForRows(
        TabState tab,
        string text,
        ListColumns columns,
        IReadOnlyList<int> rows)
    {
        var summaryStart = ListColumns.MarkWidth + columns.Key + KeySeparator + ListColumns.MarkerWidth + 1;
        var found = new List<FlashLabel>();
        var blocked = new HashSet<char>();

        foreach (var row in rows)
        {
            var issue = tab.Rows[row];

            // The key may take the label onto the gap after it; the summary stops at its column (or its ellipsis).
            var key = new Field(issue.Key, ListColumns.MarkWidth, columns.Key, KeySeparator);
            var summary = new Field(issue.Summary, summaryStart, columns.Summary, 0);

            BlockContinuations(key.Text, text, blocked);
            BlockContinuations(summary.Text, text, blocked);

            if ((Find(key, row, text) ?? Find(summary, row, text)) is { } label)
            {
                found.Add(label);
            }
        }

        found.Sort((left, right) => Compare(left.Row, right.Row, tab.Selected));
        AssignLetters(found, blocked);

        return found;
    }

    /// <summary>The label that the typed <paramref name="key"/> selects, or null when it is not a label letter.</summary>
    public static FlashLabel? Select(IReadOnlyList<FlashLabel> labels, string key)
    {
        if (key.Length != 1)
        {
            return null;
        }

        var letter = char.ToLowerInvariant(key[0]);

        foreach (var label in labels)
        {
            if (label.HasLetter && label.Letter == letter)
            {
                return label;
            }
        }

        return null;
    }

    private static FlashLabel? Find(Field field, int row, string text)
    {
        var start = field.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return null;
        }

        var shown = DisplayWidth.Of(field.Text) > field.Width ? field.Width - 1 : field.Width;
        var before = DisplayWidth.Of(field.Text[..start]);
        var through = DisplayWidth.Of(field.Text[..(start + text.Length)]);

        // A match the column cuts off (or the ellipsis hides) is not on screen.
        if (through > shown)
        {
            return null;
        }

        var fits = through + ReplacedWidth(field.Text, start + text.Length) <= shown + field.Spare;

        return new FlashLabel(
            row,
            field.Start + before,
            through - before,
            field.Start + through,
            fits ? Eligible : FlashLabel.NoLetter);
    }

    private static int ReplacedWidth(string text, int index)
    {
        if (index >= text.Length)
        {
            return 1;
        }

        Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out _);

        return Math.Max(1, DisplayWidth.Of(rune));
    }

    /// <summary>Blocks the letters that would continue a match of <paramref name="text"/> in <paramref name="subject"/>.</summary>
    internal static void BlockContinuations(string subject, string text, HashSet<char> blocked)
    {
        var start = subject.IndexOf(text, StringComparison.OrdinalIgnoreCase);

        while (start >= 0)
        {
            var next = start + text.Length;

            if (next < subject.Length)
            {
                blocked.Add(char.ToLowerInvariant(subject[next]));
                blocked.Add(char.ToUpperInvariant(subject[next]));
            }

            start = subject.IndexOf(text, start + 1, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int Compare(int left, int right, int selected)
    {
        var byDistance = Math.Abs(left - selected).CompareTo(Math.Abs(right - selected));

        return byDistance != 0 ? byDistance : left.CompareTo(right);
    }

    /// <summary>Gives the eligible labels their letters in order; a blocked letter is skipped.</summary>
    internal static void AssignLetters(List<FlashLabel> labels, HashSet<char> blocked)
    {
        var letters = Alphabet.Where(letter => !blocked.Contains(letter)).ToArray();
        var next = 0;

        for (var index = 0; index < labels.Count; index++)
        {
            if (labels[index].Letter != Eligible)
            {
                continue;
            }

            labels[index] = labels[index] with { Letter = next < letters.Length ? letters[next++] : FlashLabel.NoLetter };
        }
    }

    private readonly record struct Field(string Text, int Start, int Width, int Spare);
}
