namespace Geeratya.Core.Detail;

/// <summary>Makes Jira text safe to draw.</summary>
internal static class TextClean
{
    /// <summary>Replaces control characters, which a terminal would act on, with spaces.</summary>
    public static string Of(string text)
    {
        if (!text.Any(char.IsControl))
        {
            return text;
        }

        return string.Concat(text.Select(static character => char.IsControl(character) ? ' ' : character));
    }
}
