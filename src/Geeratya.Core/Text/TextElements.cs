using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>Editing helpers that treat a user-perceived character (a grapheme cluster) as one unit.</summary>
public static class TextElements
{
    /// <summary>The text without its last character; the text itself must not be empty.</summary>
    public static string RemoveLast(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);

        return text[..starts[^1]];
    }
}
