using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class ConnectionCheckReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");
    private static readonly JiraUser _user = new("1", "Test User 1", null, true, null, null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnActiveProfile_WhenTheCheckIsRequested_ThenTheStateIsCheckingAndTheEffectNamesTheProfile()
    {
        var result = _reducer.Reduce(Active(_work), new ConnectionCheckRequested());

        Assert.IsType<Checking>(result.State.Session.Connection);
        Assert.Equal(_work, Assert.IsType<CheckConnectionEffect>(Assert.Single(result.Effects)).Profile);
        Assert.Equal("Checking work.atlassian.net...", ConnectionText.Line(result.State.Session));
    }

    [Fact]
    public void GivenNoProfile_WhenTheStartCheckIsRequested_ThenNothingHappens()
    {
        var result = _reducer.Reduce(AppState.Initial, new ConnectionCheckRequested());

        Assert.Equal(AppState.Initial, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenNoProfile_WhenRunningProfileCheck_ThenTheStatusLineSaysToAddOne()
    {
        var result = Palette(AppState.Initial, "profile check");

        Assert.Contains(":profile add", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAFailedState_WhenRunningProfileCheck_ThenItChecksAgain()
    {
        var failed = _reducer.Reduce(Active(_work), new ConnectionCheckFailed(_work, Error(JiraErrorKind.Network))).State;

        var result = Palette(failed, "profile check");

        Assert.IsType<Checking>(result.State.Session.Connection);
        Assert.IsType<CheckConnectionEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenChecking_WhenTheUserIsReported_ThenTheStateIsConnectedWithTheDisplayName()
    {
        var checking = _reducer.Reduce(Active(_work), new ConnectionCheckRequested()).State;

        var result = _reducer.Reduce(checking, new ConnectionChecked(_work, _user));

        Assert.Equal(new Connected("Test User 1"), result.State.Session.Connection);
        Assert.Equal("Connected as Test User 1 on work.atlassian.net", ConnectionText.Line(result.State.Session));
    }

    [Fact]
    public void GivenDisplayNameWithControlCharacters_WhenTheUserIsReported_ThenTheyAreReplacedBySpaces()
    {
        var checking = _reducer.Reduce(Active(_work), new ConnectionCheckRequested()).State;
        var user = _user with { DisplayName = "Eve\u0007[2J\nX\u202E" };

        var result = _reducer.Reduce(checking, new ConnectionChecked(_work, user));

        Assert.Equal(new Connected("Eve [2J X "), result.State.Session.Connection);
    }

    [Theory]
    [InlineData(JiraErrorKind.Unauthorized, ConnectionFailureKind.Unauthorized, "Token or email rejected")]
    [InlineData(JiraErrorKind.Forbidden, ConnectionFailureKind.Forbidden, "denied access (403)")]
    [InlineData(JiraErrorKind.Network, ConnectionFailureKind.Network, "Cannot reach work.atlassian.net")]
    [InlineData(JiraErrorKind.Timeout, ConnectionFailureKind.Timeout, "did not answer in time")]
    [InlineData(JiraErrorKind.HostRefused, ConnectionFailureKind.HostRefused, "was refused")]
    [InlineData(JiraErrorKind.RateLimited, ConnectionFailureKind.RateLimited, "rate limiting")]
    [InlineData(JiraErrorKind.ServerError, ConnectionFailureKind.Other, "work.atlassian.net: boom")]
    public void GivenAFailure_WhenReducing_ThenTheStateHasTheKindAndItsHint(
        JiraErrorKind error,
        ConnectionFailureKind kind,
        string hint)
    {
        var result = _reducer.Reduce(Active(_work), new ConnectionCheckFailed(_work, Error(error)));

        var failure = Assert.IsType<ConnectionCheckFailure>(result.State.Session.Connection);

        Assert.Equal(kind, failure.Kind);
        Assert.Contains(hint, failure.Hint);
        Assert.DoesNotContain("me@work.test", failure.Hint);
    }

    [Fact]
    public void GivenARateLimitWait_WhenReducing_ThenTheHintSaysHowLong()
    {
        var error = new JiraError(JiraErrorKind.RateLimited, 429, "limited", TimeSpan.FromSeconds(12));

        var result = _reducer.Reduce(Active(_work), new ConnectionCheckFailed(_work, error));

        Assert.Contains("in 12 s", ConnectionText.Line(result.State.Session));
    }

    [Fact]
    public void GivenAReplyForAnotherProfile_WhenReducing_ThenTheStateIsUnchanged()
    {
        var checking = _reducer.Reduce(Active(_work), new ConnectionCheckRequested()).State;

        var success = _reducer.Reduce(checking, new ConnectionChecked(_dev, _user)).State;
        var failure = _reducer.Reduce(checking, new ConnectionCheckFailed(_dev, Error(JiraErrorKind.Unauthorized))).State;

        Assert.Equal(checking, success);
        Assert.Equal(checking, failure);
    }

    [Fact]
    public void GivenASwitch_WhenTheProfileChanges_ThenTheNewProfileIsCheckingAndTheOldReplyIsIgnored()
    {
        var state = Active(_work, _dev);
        var checkedWork = _reducer.Reduce(state, new ConnectionChecked(_work, _user)).State;

        var switched = _reducer.Reduce(checkedWork, new ProfilesChanged(state.Profiles.Activate("dev"), "Done."));
        var late = _reducer.Reduce(switched.State, new ConnectionChecked(_work, _user)).State;

        Assert.IsType<Checking>(switched.State.Session.Connection);
        Assert.Equal(_dev, switched.Effects.OfType<CheckConnectionEffect>().Single().Profile);
        Assert.IsType<Checking>(late.Session.Connection);
    }

    [Fact]
    public void GivenAConnectionThatCannotBeBuilt_WhenReducing_ThenTheStateIsSetupAndTheMessageIsKept()
    {
        var checking = _reducer.Reduce(Active(_work), new ConnectionCheckRequested()).State;

        var result = _reducer.Reduce(checking, new ConnectionFailed(_work, "No API token stored."));

        var failure = Assert.IsType<ConnectionCheckFailure>(result.State.Session.Connection);

        Assert.Equal(ConnectionFailureKind.Setup, failure.Kind);
        Assert.Equal("No API token stored.", failure.Hint);
        Assert.Equal("No API token stored.", result.State.Message);
    }

    private static JiraError Error(JiraErrorKind kind)
    {
        return new JiraError(kind, null, "boom");
    }

    private static AppState Active(params Profile[] profiles)
    {
        var catalog = profiles.Aggregate(ProfileCatalog.Empty, (current, profile) => current.With(profile));

        return AppState.Initial with { Profiles = catalog, Session = SiteSession.Empty(catalog.Active) };
    }

    private ReduceResult Palette(AppState state, string text)
    {
        var typed = state with { Mode = InputMode.Command, PaletteText = text };

        return _reducer.Reduce(typed, new KeyPressed("Enter"));
    }
}
