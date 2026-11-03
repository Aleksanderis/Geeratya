using Geeratya.App.PrintFrame;
using Geeratya.Jira;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.App.Tests.PrintFrame;

public sealed class AllowListedTransportFactoryTests
{
    [Fact]
    public async Task GivenHostOutsideTheList_WhenCallingThroughTheTransport_ThenItIsRefusedBeforeAnyRequest()
    {
        var factory = new AllowListedTransportFactory(new HashSet<string> { "dev.example.net" });
        var connection = new JiraConnection(new Uri("https://work.example.net"), "user@example.com");
        using var transport = factory.Create(connection, new JiraApiToken("placeholder"));

        var result = await new JiraUserClient(transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.HostRefused, result.Error.Kind);
    }

    [Fact]
    public async Task GivenAnEmptyList_WhenCallingThroughTheTransport_ThenEveryHostIsRefused()
    {
        var factory = new AllowListedTransportFactory(new HashSet<string>());
        var connection = new JiraConnection(new Uri("https://dev.example.net"), "user@example.com");
        using var transport = factory.Create(connection, new JiraApiToken("placeholder"));

        var result = await new JiraUserClient(transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.HostRefused, result.Error.Kind);
    }
}
