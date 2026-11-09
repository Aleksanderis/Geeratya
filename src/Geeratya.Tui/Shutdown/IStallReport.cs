namespace Geeratya.Tui.Shutdown;

/// <summary>Where the stall monitor writes what it saw.</summary>
public interface IStallReport
{
    /// <summary>
    /// The UI thread has not turned its loop for <paramref name="stalled"/>; <paramref name="lastWork"/> names what
    /// it started last.
    /// </summary>
    void Stalled(TimeSpan stalled, string lastWork);

    /// <summary>The loop turns again after a stall that lasted about <paramref name="stalled"/>.</summary>
    void Recovered(TimeSpan stalled);
}
