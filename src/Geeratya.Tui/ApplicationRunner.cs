using Geeratya.App;
using Geeratya.Core;
using Geeratya.Core.Keymap;
using Geeratya.Platform;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui;

/// <summary>Entry logic behind <c>Main</c>, testable without a process.</summary>
public sealed partial class ApplicationRunner
{
    private const string VersionFlag = "--version";
    private const string PrintKeymapFlag = "--print-keymap-doc";
    private const string PrintFrameFlag = "--print-frame";
    private const string SizeFlag = "--size";
    private const string ModeFlag = "--mode";
    private const string HelpScrollFlag = "--help-scroll";
    private const string HelpPageFlag = "--help-page";
    private const string LevelFlag = "--command-level";
    private const string ColorsFlag = "--colors";
    private const string SourceFlag = "--source";
    private const string LayoutFlag = "--layout";
    private const string ProfileFlag = "--profile";
    private const string LightFlag = "--light";
    private const string AsciiFlag = "--ascii";

    private const string HelpFlag = "--help";
    private const string ShortHelpFlag = "-h";

    private static readonly string[] _bareFlags =
        [VersionFlag, PrintKeymapFlag, PrintFrameFlag, LightFlag, AsciiFlag, HelpFlag, ShortHelpFlag];

    private static readonly string[] _valueFlags =
        [SizeFlag, ModeFlag, HelpScrollFlag, HelpPageFlag, LevelFlag, ColorsFlag, SourceFlag, LayoutFlag, ProfileFlag];

    /// <summary>The single usage text; docs quote it and <c>--help</c> prints it.</summary>
    public static readonly string Usage = string.Join(
        '\n',
        "Usage: geeratya [options]",
        "",
        "Options:",
        $"  {ShortHelpFlag}, {HelpFlag}        Print this usage and exit.",
        $"  {VersionFlag}             Print the name and version and exit.",
        $"  {PrintKeymapFlag}  Print the key reference as Markdown and exit.",
        $"  {PrintFrameFlag}        Print the plain-text frame the shell would draw and exit.",
        $"      [{SizeFlag} <width>x<height>]                  default 80x24",
        $"      [{ModeFlag} normal|help|command|confirm|insert]",
        $"      [{HelpPageFlag} <page>] [{HelpScrollFlag} <line>]   need {ModeFlag} help",
        $"      [{LevelFlag} everyday|advanced]",
        $"      [{ColorsFlag} truecolor|16|none] [{LightFlag}] [{AsciiFlag}]",
        "          write ANSI colours (none: attributes only); --light needs theme auto or no theme",
        $"      [{SourceFlag} board:<name>|jql:<query>|issue:<KEY>] [{LayoutFlag} list|columns] [{ProfileFlag} <name>]",
        "          draw a real screen from a profile (a fake Jira); its site must be in GEERATYA_LIVE_HOSTS;",
        $"          {ProfileFlag} and {LayoutFlag} need {SourceFlag}");

    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly bool _isInteractive;
    private readonly IProfileServicesFactory _profileFactory;
    private readonly ColorSupport _colors;
    private readonly GlyphSet _glyphs;
    private readonly TerminalBackgroundDetector? _background;
    private readonly EnvironmentReader _environment;
    private readonly ITransportFactory? _transports;

    public ApplicationRunner(
        RunnerContext context,
        IProfileServicesFactory? profileFactory = null,
        ITransportFactory? transports = null)
    {
        _environment = context.Environment ?? EnvironmentReader.ForProcess();
        _transports = transports;
        _background = context.Background;
        _output = context.Output;
        _error = context.Error ?? Console.Error;
        _isInteractive = context.IsInteractive;
        _colors = context.Colors;
        _glyphs = context.Glyphs;
        _profileFactory = profileFactory ?? new ProcessProfileServicesFactory();
    }

    public int Run(string[] arguments)
    {
        var unknown = FindUnknownArgument(arguments);

        if (arguments.Contains(HelpFlag) || arguments.Contains(ShortHelpFlag))
        {
            _output.WriteLine(Usage);

            return 0;
        }

        if (unknown is not null)
        {
            _error.WriteLine($"{ApplicationInfo.Name}: unknown argument '{unknown}'");
            _error.WriteLine(Usage);

            return 2;
        }

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
            profiles = _profileFactory.Create();
        }
        catch (InvalidOperationException exception)
        {
            _error.WriteLine($"{ApplicationInfo.Name}: {exception.Message}");

            return 1;
        }

        using var host = new ShellHost(keymap, _colors, profiles, _glyphs, _background);

        return host.Run();
    }

    private static string? FindUnknownArgument(string[] arguments)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (Array.IndexOf(_valueFlags, argument) >= 0)
            {
                index++;
            }
            else if (Array.IndexOf(_bareFlags, argument) < 0)
            {
                return argument;
            }
        }

        return null;
    }

    private static string? ValueOf(string[] arguments, string flag)
    {
        var index = Array.IndexOf(arguments, flag);

        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
