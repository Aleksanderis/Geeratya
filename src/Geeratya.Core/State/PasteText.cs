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
}
