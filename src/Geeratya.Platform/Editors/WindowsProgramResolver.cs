namespace Geeratya.Platform.Editors;

/// <summary>
/// Finds a program the way the Windows command prompt does: each folder of <c>PATH</c>, and in each the name as
/// given (when it already ends in a <c>PATHEXT</c> extension) and then with every <c>PATHEXT</c> extension appended.
/// This is what makes <c>EDITOR=code</c> work: the process API alone finds <c>.exe</c> files only, and VS Code
/// puts <c>code.cmd</c> on <c>PATH</c>. Paths are built with a backslash by hand so the lookup is tested on any host.
/// </summary>
public sealed class WindowsProgramResolver
{
    private const string DefaultExtensions = ".COM;.EXE;.BAT;.CMD";
    private const char Separator = ';';

    private readonly EnvironmentReader _environment;
    private readonly IFileProbe _probe;

    public WindowsProgramResolver(EnvironmentReader environment, IFileProbe probe)
    {
        _environment = environment;
        _probe = probe;
    }

    /// <summary>Whether the file is a batch script, which must run through the command interpreter.</summary>
    public static bool IsBatchFile(string program)
    {
        return program.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || program.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The full path of the program, or the name unchanged when nothing matches.</summary>
    public string Resolve(string program)
    {
        var extensions = Extensions();
        var hasExtension = extensions.Any(extension => program.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        foreach (var directory in Directories(program))
        {
            var baseName = directory.Length == 0 ? program : $"{directory.TrimEnd('\\', '/')}\\{program}";

            if (hasExtension && _probe.FileExists(baseName))
            {
                return baseName;
            }

            foreach (var extension in extensions)
            {
                var candidate = $"{baseName}{extension}";

                if (_probe.FileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return program;
    }

    private List<string> Extensions()
    {
        var configured = _environment.Get("PATHEXT") ?? DefaultExtensions;

        return [.. configured.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private IEnumerable<string> Directories(string program)
    {
        var hasDirectory = program.IndexOfAny(['\\', '/', ':']) >= 0;

        if (hasDirectory)
        {
            return [string.Empty];
        }

        var path = _environment.Get("PATH") ?? string.Empty;

        return path
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Trim('"'))
            .Where(entry => entry.Length > 0);
    }
}
