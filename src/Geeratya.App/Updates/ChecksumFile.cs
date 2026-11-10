namespace Geeratya.App.Updates;

/// <summary>Reads a release's <c>SHA256SUMS</c>: lines of <c>hash  name</c> (or <c>hash *name</c>).</summary>
public static class ChecksumFile
{
    /// <summary>The hash listed for <paramref name="fileName"/>, lower case, or null when it has no line.</summary>
    public static string? HashOf(string text, string fileName)
    {
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Trim().Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2 && parts[1].Trim().TrimStart('*') == fileName)
            {
                return parts[0].ToLowerInvariant();
            }
        }

        return null;
    }
}
