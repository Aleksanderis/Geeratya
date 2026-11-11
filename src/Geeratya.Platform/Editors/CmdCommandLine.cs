namespace Geeratya.Platform.Editors;

/// <summary>
/// Builds the argument string that runs a batch file (<c>.cmd</c>, <c>.bat</c>) through <c>cmd.exe</c>:
/// <c>/d /s /c ""script" "arg" "arg""</c>. With <c>/s</c> the command interpreter strips the outer pair of quotes and
/// keeps the inner ones, so paths with spaces work. A double quote or a percent sign inside a word cannot be quoted
/// safely for <c>cmd</c> (quotes cannot be escaped, <c>%NAME%</c> expands even inside quotes), so such a command is
/// refused rather than passed on.
/// </summary>
public static class CmdCommandLine
{
    /// <summary>
    /// The argument string for <c>cmd.exe</c>; throws <see cref="NotSupportedException"/> for an unsafe word.
    /// </summary>
    public static string Build(string script, IReadOnlyList<string> arguments)
    {
        var words = new List<string> { script };

        words.AddRange(arguments);

        foreach (var word in words)
        {
            if (word.Contains('"') || word.Contains('%') || word.Contains('\r') || word.Contains('\n'))
            {
                throw new NotSupportedException("A batch file argument contains a quote, percent sign or line break.");
            }
        }

        return $"/d /s /c \"{string.Join(' ', words.Select(Quote))}\"";
    }

    private static string Quote(string word)
    {
        return $"\"{word}\"";
    }
}
