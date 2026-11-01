using Geeratya.Core.Profiles;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

public sealed class JiraServicesTests
{
    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");

    [Fact]
    public async Task GivenConnectedSite_WhenCreatingServices_ThenTheyCarryTransportAndGeneration()
    {
        using var connections = await ConnectedAsync();

        var services = JiraServices.Create(connections);

        Assert.Same(connections.Current, services!.Transport);
        Assert.True(connections.IsCurrent(services.Generation));
    }

    [Fact]
    public void GivenNoConnection_WhenCreatingServices_ThenNull()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        Assert.Null(JiraServices.Create(connections));
    }

    [Fact]
    public async Task GivenServicesBeforeSwitch_WhenSwitched_ThenTheirGenerationIsStale()
    {
        using var connections = await ConnectedAsync();
        var services = JiraServices.Create(connections)!;

        await connections.ActivateAsync(_b, 2, CancellationToken.None);

        Assert.False(connections.IsCurrent(services.Generation));
    }

    private static async Task<SiteConnections> ConnectedAsync()
    {
        var store = new FakeSecretStore();

        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);
        await store.SetAsync(_b.CredentialKey, "token-b", CancellationToken.None);

        var connections = new SiteConnections(new TokenProvider(store, new EnvironmentReader(_ => null)));

        await connections.ActivateAsync(_a, 2, CancellationToken.None);

        return connections;
    }
}
