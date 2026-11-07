using System.Text;

namespace Geeratya.Platform.Editors;

/// <summary>
/// Splits an <c>$EDITOR</c> value such as <c>code --wait</c> or <c>"C:\Program Files\Editor\ed.exe" -w</c> into the
/// program and its arguments. Words are separated by spaces; single or double quotes group words and are removed.
/// There are no escapes (a backslash is a path character on Windows) and no shell expansion.
/// </summary>
public static class EditorCommandLine
{
    public static IReadOnlyList<string> Split(string value)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var started = false;
        var quote = '\0';

        foreach (var character in value)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
                started = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                Flush(words, current, ref started);
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }

        Flush(words, current, ref started);

        return words;
    }

    private static void Flush(List<string> words, StringBuilder current, ref bool started)
    {
        if (started)
        {
            words.Add(current.ToString());
        }

        current.Clear();
        started = false;
    }
}
