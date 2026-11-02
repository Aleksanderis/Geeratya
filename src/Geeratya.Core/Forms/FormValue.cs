namespace Geeratya.Core.Forms;

/// <summary>
/// The value of one field: the text for the text kinds, the chosen ids for the choice kinds, the labels for labels.
/// Plain data, compared by content.
/// </summary>
public sealed record FormValue(string Text, IReadOnlyList<string> Chosen)
{
    public static FormValue Empty { get; } = new(string.Empty, []);

    /// <summary>True when there is no text and nothing is chosen.</summary>
    public bool IsEmpty => Text.Trim().Length == 0 && Chosen.Count == 0;

    public static FormValue OfText(string text)
    {
        return new FormValue(text, []);
    }

    public static FormValue OfChosen(IEnumerable<string> chosen)
    {
        return new FormValue(string.Empty, [.. chosen]);
    }

    public bool Equals(FormValue? other)
    {
        return other is not null && Text == other.Text && Chosen.SequenceEqual(other.Chosen);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Text, Chosen.Count);
    }
}
