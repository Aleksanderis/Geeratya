using Geeratya.Jira.Users;

namespace Geeratya.Jira.Tests.Live;

public class LiveMyselfTests
{
    [LiveFact]
    public async Task GivenDevSite_WhenGettingMyself_ThenTheAccountOfTheTokenIsReturned()
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var client = new JiraUserClient(transport);

        var result = await client.GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsSuccess ? null : $"Jira replied {result.Error.Kind}.");
        Assert.False(string.IsNullOrEmpty(result.Value.AccountId));
        Assert.True(result.Value.Active);
    }
}
