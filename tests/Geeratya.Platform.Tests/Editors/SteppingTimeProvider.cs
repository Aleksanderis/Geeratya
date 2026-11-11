namespace Geeratya.Platform.Tests.Editors;

/// <summary>
/// A clock that moves only when it is read: every timestamp is <c>step</c> later than the one before, so a test
/// decides how long a measured span "took" without depending on how fast the machine is.
/// </summary>
public sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        var current = _ticks;

        _ticks += step.Ticks;

        return current;
    }
}
