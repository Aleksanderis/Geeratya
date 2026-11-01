using Geeratya.Core;
using Geeratya.Core.Keymap;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui;

/// <summary>Entry logic behind <c>Main</c>, testable without a process.</summary>
public sealed class ApplicationRunner
{
    private const string VersionFlag = "--version";
    private const string PrintKeymapFlag = "--print-keymap-doc";
    private const string PrintFrameFlag = "--print-frame";
    private const string SizeFlag = "--size";
    private const string ModeFlag = "--mode";

    private readonly TextWriter _output;
    private readonly bool _isInteractive;
    private readonly ColorSupport _colors;

    public ApplicationRunner(TextWriter output, bool isInteractive, ColorSupport colors = ColorSupport.TrueColor)
    {
        _colors = colors;
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

        if (arguments.Contains(PrintFrameFlag))
        {
            return PrintFrame(arguments, keymap);
        }

        if (!_isInteractive)
        {
            _output.WriteLine($"{ApplicationInfo.Name} needs an interactive terminal.");

            return 1;
        }

        return new ShellHost(keymap, _colors).Run();
    }

    private int PrintFrame(string[] arguments, KeymapTable keymap)
    {
        var size = FrameOptions.ParseSize(ValueOf(arguments, SizeFlag));
        var mode = FrameOptions.ParseMode(ValueOf(arguments, ModeFlag));

        if (size is null || mode is null)
        {
            _output.WriteLine(
                $"Usage: {PrintFrameFlag} [{SizeFlag} <width>x<height>] [{ModeFlag} normal|help|command|confirm]");

            return 2;
        }

        var state = FrameOptions.SampleState(mode.Value);
        var lines = FrameBuilder.Build(state, size.Width, size.Height, KeymapDocument.RenderHelpLines(keymap));

        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }

        return 0;
    }

    private static string? ValueOf(string[] arguments, string flag)
    {
        var index = Array.IndexOf(arguments, flag);

        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
