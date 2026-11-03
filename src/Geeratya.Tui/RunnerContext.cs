using Geeratya.Platform;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui;

/// <summary>What <see cref="ApplicationRunner"/> needs from the process around it: streams, terminal facts, environment.</summary>
/// <param name="Output">Where results and usage go.</param>
/// <param name="IsInteractive">True when stdin and stdout are a terminal the UI can start in.</param>
/// <param name="Colors">The colour tier of the terminal.</param>
/// <param name="Error">Where problems go; the process error stream when null.</param>
/// <param name="Glyphs">The glyph set of the terminal.</param>
/// <param name="Background">Detects a light terminal background; null when not asked.</param>
/// <param name="Environment">Reads environment variables; the process environment when null.</param>
public sealed record RunnerContext(
    TextWriter Output,
    bool IsInteractive,
    ColorSupport Colors = ColorSupport.TrueColor,
    TextWriter? Error = null,
    GlyphSet Glyphs = GlyphSet.Unicode,
    TerminalBackgroundDetector? Background = null,
    EnvironmentReader? Environment = null);
