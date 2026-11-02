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
    private readonly TextWriter _error;
    private readonly bool _isInteractive;
    private readonly Func<ProfileServices> _createProfiles;
    private readonly ColorSupport _colors;
    private readonly GlyphSet _glyphs;

    public ApplicationRunner(
        TextWriter output,
        bool isInteractive,
        ColorSupport colors = ColorSupport.TrueColor,
        TextWriter? error = null,
        Func<ProfileServices>? createProfiles = null,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        _output = output;
        _error = error ?? Console.Error;
        _isInteractive = isInteractive;
        _colors = colors;
        _glyphs = glyphs;
        _createProfiles = createProfiles ?? ProfileServices.CreateForProcess;
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

        return RunShell(keymap);
    }

    private int RunShell(KeymapTable keymap)
    {
        ProfileServices profiles;

        try
        {
            profiles = _createProfiles();
        }
        catch (InvalidOperationException exception)
        {
            _error.WriteLine($"{ApplicationInfo.Name}: {exception.Message}");

            return 1;
        }

        using var host = new ShellHost(keymap, _colors, profiles, _glyphs);

        return host.Run();
    }

    private int PrintFrame(string[] arguments, KeymapTable keymap)
    {
        var size = FrameOptions.ParseSize(ValueOf(arguments, SizeFlag));
        var mode = FrameOptions.ParseMode(ValueOf(arguments, ModeFlag));

        if (size is null || mode is null)
        {
            _output.WriteLine(
                $"Usage: {PrintFrameFlag} [{SizeFlag} <width>x<height>] [{ModeFlag} normal|help|command|confirm|insert]");

            return 2;
        }

        var state = FrameOptions.SampleState(mode.Value);
        var helpLines = KeymapDocument.RenderHelpLines(keymap);
        var lines = FrameBuilder.Build(state, size.Width, size.Height, helpLines, keymap, null, _glyphs);

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
