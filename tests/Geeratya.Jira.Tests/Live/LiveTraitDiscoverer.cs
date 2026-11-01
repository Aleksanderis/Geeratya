using Xunit.Abstractions;
using Xunit.Sdk;

namespace Geeratya.Jira.Tests.Live;

/// <summary>Adds the trait <c>Live=true</c> to every <see cref="LiveFactAttribute"/> test.</summary>
public sealed class LiveTraitDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute) =>
        [new KeyValuePair<string, string>("Live", "true")];
}
