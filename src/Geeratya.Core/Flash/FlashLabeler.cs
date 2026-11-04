using System.Text;
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

    private const char Eligible = '\u0001';
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
        var summaryStart = ListColumns.MarkWidth + columns.Key + KeySeparator + ListColumns.MarkerWidth + 1;
        var first = Math.Clamp(tab.Scroll, 0, tab.Rows.Count - 1);
        var end = Math.Min(tab.Rows.Count, first + area);
        var found = new List<FlashLabel>();
        var blocked = new HashSet<char>();

        for (var row = first; row < end; row++)
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

    private static void BlockContinuations(string subject, string text, HashSet<char> blocked)
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

    private static void AssignLetters(List<FlashLabel> labels, HashSet<char> blocked)
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
