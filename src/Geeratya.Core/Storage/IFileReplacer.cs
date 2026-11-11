namespace Geeratya.Core.Storage;

/// <summary>
/// The file system step of an atomic write that can fail on a sharing violation, and the wait between retries.
/// A seam so tests can simulate a locked target on any host.
/// </summary>
public interface IFileReplacer
{
    /// <summary>Moves <paramref name="temporary"/> over <paramref name="target"/>, replacing it.</summary>
    void Replace(string temporary, string target);

    /// <summary>Waits before the next attempt.</summary>
    void Pause(TimeSpan duration);
}
