using Xunit.Sdk;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// A test against the owner's dev site. Skipped with a reason when the <c>GEERATYA_LIVE_*</c> variables are missing;
/// carries the trait <c>Live=true</c> so <c>--filter "Live=true"</c> selects it. The transport refuses hosts outside
/// <c>GEERATYA_LIVE_HOSTS</c>.
/// </summary>
[TraitDiscoverer("Geeratya.Jira.Tests.Live.LiveTraitDiscoverer", "Geeratya.Jira.Tests")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveFactAttribute : FactAttribute, ITraitAttribute
{
    public LiveFactAttribute()
    {
        Skip = LiveEnvironment.MissingReason(Environment.GetEnvironmentVariable);
    }
}
