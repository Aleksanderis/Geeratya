using Geeratya.Platform;
using Geeratya.Platform.Terminal;
using Geeratya.Tui;

var capabilities = ConsoleCapabilities.Detect(
    EnvironmentReader.ForProcess(),
    Console.IsInputRedirected,
    Console.IsOutputRedirected);

return new ApplicationRunner(
    Console.Out,
    capabilities.IsInteractive,
    capabilities.Colors,
    glyphs: capabilities.Glyphs).Run(args);
