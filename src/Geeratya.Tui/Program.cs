using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Terminal;
using Geeratya.Tui;

var environment = EnvironmentReader.ForProcess();
var operatingSystem = ApplicationDirectories.DetectOperatingSystem();
var capabilities = ConsoleCapabilities.Detect(
    environment,
    Console.IsInputRedirected,
    Console.IsOutputRedirected,
    operatingSystem);
var background = new TerminalBackgroundDetector(
    environment,
    capabilities.IsInteractive,
    operatingSystem,
    new ConsoleBackgroundQuery(environment));

var context = new RunnerContext(
    Console.Out,
    capabilities.IsInteractive,
    capabilities.Colors,
    Glyphs: capabilities.Glyphs,
    Background: background);

return new ApplicationRunner(context).Run(args);
