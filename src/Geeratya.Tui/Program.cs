using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Terminal;
using Geeratya.Tui;

var capabilities = ConsoleCapabilities.Detect(
    EnvironmentReader.ForProcess(),
    Console.IsInputRedirected,
    Console.IsOutputRedirected,
    ApplicationDirectories.DetectOperatingSystem());

return new ApplicationRunner(
    Console.Out,
    capabilities.IsInteractive,
    capabilities.Colors,
    glyphs: capabilities.Glyphs).Run(args);
