using Geeratya.Core;
using Geeratya.Core.Keymap;

namespace Geeratya.Tui;

/// <summary>Entry logic behind <c>Main</c>, testable without a process.</summary>
public sealed class ApplicationRunner
{
    private const string VersionFlag = "--version";
    private const string PrintKeymapFlag = "--print-keymap-doc";

    private readonly TextWriter _output;
    private readonly bool _isInteractive;

    public ApplicationRunner(TextWriter output, bool isInteractive)
    {
        _output = output;
        _isInteractive = isInteractive;
    }

    public int Run(string[] arguments)
    {
        if (arguments.Contains(VersionFlag))
        {
            _output.WriteLine($"{ApplicationInfo.Name} {ApplicationInfo.Version}");

            return 0;
        }

        var keymap = DefaultKeymap.Load();

        if (arguments.Contains(PrintKeymapFlag))
        {
            _output.Write(KeymapDocument.RenderMarkdown(keymap));

            return 0;
        }

        if (!_isInteractive)
        {
            _output.WriteLine($"{ApplicationInfo.Name} needs an interactive terminal.");

            return 1;
        }

        return new ShellHost(keymap).Run();
    }
}
