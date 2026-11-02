using System.Text;

namespace Geeratya.Core.State;

/// <summary>Cleans pasted text for a single-line field.</summary>
public static class PasteText
{
    /// <summary>The first non-empty line of <paramref name="pasted"/> without control characters.</summary>
    public static string FirstLine(string pasted)
    {
        foreach (var line in pasted.Split(['\r', '\n']))
        {
            var builder = new StringBuilder(line.Length);

            foreach (var character in line)
            {
                if (character == (char)9)
                {
                    builder.Append(char.ConvertFromUtf32(32));
                }
                else if (!char.IsControl(character))
                {
                    builder.Append(character);
                }
            }

            if (builder.Length > 0)
            {
                var trimmed = builder.ToString().Trim();

                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// All lines of <paramref name="pasted"/> for a multi-line editor: line breaks become <c>\n</c>, a tab becomes
    /// four spaces and other control characters are dropped.
    /// </summary>
    public static string Lines(string pasted)
    {
        var builder = new StringBuilder(pasted.Length);
        var normalized = pasted.Replace("\r\n", "\n").Replace('\r', '\n');

        foreach (var character in normalized)
        {
            if (character == '\n')
            {
                builder.Append(character);
            }
            else if (character == '\t')
            {
                builder.Append("    ");
            }
            else if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
