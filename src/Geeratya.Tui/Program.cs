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
    new ConsoleBackgroundQuery());

return new ApplicationRunner(
    Console.Out,
    capabilities.IsInteractive,
    capabilities.Colors,
    glyphs: capabilities.Glyphs,
    background: background).Run(args);
