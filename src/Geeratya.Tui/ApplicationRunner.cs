using Geeratya.Core;

namespace Geeratya.Tui;

/// <summary>Entry logic behind <c>Main</c>, testable without a process.</summary>
public sealed class ApplicationRunner
{
    private readonly TextWriter _output;

    public ApplicationRunner(TextWriter output)
    {
        _output = output;
    }

    public int Run(string[] arguments)
    {
        if (arguments.Contains("--version"))
        {
            _output.WriteLine($"{ApplicationInfo.Name} {ApplicationInfo.Version}");

            return 0;
        }

        _output.WriteLine($"{ApplicationInfo.Name} {ApplicationInfo.Version}: the shell is not built yet.");

        return 0;
    }
}
