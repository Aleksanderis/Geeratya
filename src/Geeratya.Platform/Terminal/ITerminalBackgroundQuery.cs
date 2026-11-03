namespace Geeratya.Platform.Terminal;

/// <summary>Asks the attached terminal for its background colour (OSC 11); a seam so tests can play the terminal.</summary>
public interface ITerminalBackgroundQuery
{
    /// <summary>The raw answer text, null when the terminal did not answer in time.</summary>
    string? Ask(TimeSpan timeout);
}
