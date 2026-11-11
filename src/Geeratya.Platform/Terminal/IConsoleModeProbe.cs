namespace Geeratya.Platform.Terminal;

/// <summary>Asks the console whether it interprets escape sequences written to it.</summary>
public interface IConsoleModeProbe
{
    /// <summary>
    /// True when output escape sequences are processed, false when they would print as literal text (a classic
    /// Windows console host with virtual terminal processing off, or no console handle at all).
    /// </summary>
    bool OutputSequencesEnabled();
}
