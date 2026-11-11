using Geeratya.Core.Profiles;

namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public class ConnectionDemandTests
{
    private static readonly Profile _profile = new("dev", "https://dev.example.net", "me@example.net");

    [Fact]
    public void GivenTheFirstCall_WhenAsking_ThenTheConnectionIsNeeded()
    {
        Assert.True(new ConnectionDemand().Needed(_profile, 1, false));
    }

    [Fact]
    public void GivenTheSameProfileAndCount_WhenAKeyDispatchAsks_ThenNothingIsNeeded()
    {
        var demand = new ConnectionDemand();

        demand.Needed(_profile, 1, false);

        Assert.False(demand.Needed(_profile, 1, false));
        Assert.False(demand.Needed(_profile, 1, false));
    }

    [Fact]
    public void GivenAnotherProfileOrCount_WhenAsking_ThenTheConnectionIsNeeded()
    {
        var demand = new ConnectionDemand();

        demand.Needed(_profile, 1, false);

        Assert.True(demand.Needed(_profile, 2, false));
        Assert.True(demand.Needed(_profile with { Email = "other@example.net" }, 2, false));
        Assert.True(demand.Needed(null, 2, false));
    }

    [Fact]
    public void GivenARetryRequest_WhenNothingChanged_ThenTheConnectionIsNeeded()
    {
        var demand = new ConnectionDemand();

        demand.Needed(_profile, 1, false);

        Assert.True(demand.Needed(_profile, 1, true));
    }
}
