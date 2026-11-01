namespace Geeratya.Jira.Tests.Fakes;

/// <summary>A clock that always reports the same instant.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
