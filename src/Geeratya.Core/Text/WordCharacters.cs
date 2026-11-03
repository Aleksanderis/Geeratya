using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>What counts as part of a word for word moves and word deletes (the comment editor and the prompts).</summary>
public static class WordCharacters
{
    /// <summary>Letters, digits and underscores, plus the parts of one character (surrogates, marks, joiners).</summary>
    public static bool IsWord(char value)
    {
        if (char.IsLetterOrDigit(value) || value == '_' || char.IsSurrogate(value))
        {
            return true;
        }

        // Marks and joiners (a zero-width joiner is a format character) keep a grapheme cluster in one piece.
        return CharUnicodeInfo.GetUnicodeCategory(value)
            is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format;
    }
}
