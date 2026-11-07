using Geeratya.Jira.Jql;
using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class SuggestionHandlerTests
{
    private const string SiteLabels = """{"values":["backend-site","frontend-site"],"isLast":true}""";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _other = new("b", "https://b.atlassian.net", "me@b.test");
    private static readonly TabId _tab = new(1);
    private static readonly SuggestionSource _assignee = new(SuggestionKind.AssignableUsers, "A-1", []);

    [Fact]
    public async Task GivenAUserQuery_WhenTheDebounceHasNotPassed_ThenNothingIsSentUntilItDoes()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(249));

        Assert.Empty(fixture.Http.Requests);
        Assert.False(task.IsCompleted);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        await task;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        var request = Assert.Single(fixture.Http.Requests);
        Assert.Equal(1, loaded.RequestId);
        Assert.Equal("fake-account-1", Assert.Single(loaded.Items).Id);
        Assert.EndsWith("/rest/api/3/user/assignable/search", request.AbsolutePath);
        Assert.Contains("issueKey=A-1", request.Query);
        Assert.Contains("query=jo", request.Query);
    }

    [Fact]
    public async Task GivenTwoQueriesInARow_WhenTheSecondStarts_ThenTheFirstIsCancelledAndOnlyOneRequestIsSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var first = fixture.Handler.StartAsync(Effect(1, "j"));
        await fixture.WaitForTimersAsync(1);
        var second = fixture.Handler.StartAsync(Effect(2, "jo"));
        await first;
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await second;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(2, loaded.RequestId);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenAQueryWaitingForTheDebounce_WhenTheTabCancels_ThenNoRequestIsSentAndNothingIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Handler.Cancel(_tab);
        await task;

        Assert.Empty(fixture.Http.Requests);
        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenALookupThatTakesLong_WhenTheSlowTimeIsReached_ThenSlowIsPostedBeforeTheAnswer()
    {
        var gate = new TaskCompletionSource();
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users(), gate.Task));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(150));
        await fixture.WaitForPostedAsync(1);

        Assert.IsType<SuggestionsSlow>(Assert.Single(fixture.Posted));

        gate.SetResult();
        await task;

        Assert.IsType<SuggestionsLoaded>(fixture.Posted[1]);
    }

    [Fact]
    public async Task GivenAFastLookup_WhenItAnswers_ThenNoSlowNoticeIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        Assert.DoesNotContain(fixture.Posted, command => command is SuggestionsSlow);
    }

    [Fact]
    public async Task GivenAServerError_WhenTheLookupFails_ThenTheFailureCarriesTheRequestId()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, "{}"));

        var task = fixture.Handler.StartAsync(Effect(5, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        var failed = Assert.IsType<SuggestionsFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(5, failed.RequestId);
    }

    [Fact]
    public async Task GivenALabelQuery_WhenTheSiteAnswers_ThenKnownAndSiteLabelsAreBothOffered()
    {
        var site = """{"values":["backend-site"],"isLast":true}""";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, site));
        var source = new SuggestionSource(SuggestionKind.Labels, null, ["backend-view"]);

        var task = fixture.Handler.StartAsync(new SearchSuggestionsEffect(_tab, 3, source, "backend", TimeSpan.FromMilliseconds(250)));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["backend-site", "backend-view"], loaded.Items.Select(item => item.Label).Order());
    }

    [Fact]
    public async Task GivenALabelTypedCharacterByCharacter_WhenLookedUp_ThenTheSiteIsReadOnce()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, SiteLabels));

        await LookUpLabelsAsync(fixture, 1, "b");
        await LookUpLabelsAsync(fixture, 2, "ba");
        await LookUpLabelsAsync(fixture, 3, "bac");

        Assert.Single(fixture.Http.Requests);
        Assert.Equal(3, fixture.Posted.Count);
        Assert.All(fixture.Posted, command => Assert.IsType<SuggestionsLoaded>(command));
        Assert.Equal("backend-site", Assert.Single(((SuggestionsLoaded)fixture.Posted[2]).Items).Label);
    }

    [Fact]
    public async Task GivenASlowLabelRead_WhenTheNextKeystrokeCancelsTheLookup_ThenTheReadIsSharedNotRestarted()
    {
        var gate = new TaskCompletionSource();
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, SiteLabels, gate.Task));
        var source = new SuggestionSource(SuggestionKind.Labels, null, []);

        var first = fixture.Handler.StartAsync(
            new SearchSuggestionsEffect(_tab, 1, source, "b", TimeSpan.FromMilliseconds(250)));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await fixture.WaitForRequestsAsync(1);

        var second = fixture.Handler.StartAsync(new SearchSuggestionsEffect(_tab, 2, source, "ba", TimeSpan.Zero));
        await first;
        gate.SetResult();
        await second;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(2, loaded.RequestId);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenACachedLabelList_WhenItsLifetimeEnds_ThenTheNextLookupReadsTheSiteAgain()
    {
        using var fixture = await ConnectedAsync(TwoSiteReads);

        await LookUpLabelsAsync(fixture, 1, "b");
        fixture.Time.Advance(SiteLabelCache.Lifetime + TimeSpan.FromSeconds(1));
        await LookUpLabelsAsync(fixture, 2, "ba");

        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    [Fact]
    public async Task GivenACachedLabelList_WhenTheProfileIsSwitched_ThenTheNewConnectionReadsTheSiteAgain()
    {
        using var fixture = await ConnectedAsync(TwoSiteReads);

        await LookUpLabelsAsync(fixture, 1, "b");
        await fixture.Connections.ActivateAsync(_other, 2, CancellationToken.None);
        await LookUpLabelsAsync(fixture, 2, "ba");

        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    [Fact]
    public async Task GivenAFailedLabelRead_WhenLookedUpAgain_ThenTheFailureIsNotCached()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.InternalServerError, "{}").Reply(HttpStatusCode.OK, SiteLabels));

        await LookUpLabelsAsync(fixture, 1, "b");
        await LookUpLabelsAsync(fixture, 2, "ba");

        Assert.IsType<SuggestionsFailed>(fixture.Posted[0]);
        Assert.IsType<SuggestionsLoaded>(fixture.Posted[1]);
        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    [Fact]
    public async Task GivenAParentQuery_WhenLookedUp_ThenTheIssuePickerIsAsked()
    {
        var picker = RecordedFixtures.Read("suggestions/issue-picker.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, picker));
        var source = new SuggestionSource(SuggestionKind.Issues, "A-1", []);

        var task = fixture.Handler.StartAsync(new SearchSuggestionsEffect(_tab, 4, source, "login", TimeSpan.FromMilliseconds(250)));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(2, fixture.Http.Requests.Count);
        Assert.EndsWith("/rest/api/3/issue/picker", fixture.Http.Requests.First().AbsolutePath);
    }

    [Fact]
    public async Task GivenAParentQuery_WhenTheTypeSearchAnswers_ThenEveryOfferedIssueCarriesItsType()
    {
        var picker = RecordedFixtures.Read("suggestions/issue-picker.json");
        var types = RecordedFixtures.Read("suggestions/issue-types.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, picker).Reply(HttpStatusCode.OK, types));
        var source = new SuggestionSource(SuggestionKind.Issues, "A-1", []);

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 4, source, "login", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["Story", "Release blocker 日本"], loaded.Items.Select(item => item.Kind));
    }

    [Fact]
    public async Task GivenALinkFormQuery_WhenLookedUp_ThenTheIssueItselfIsNotOffered()
    {
        var picker = RecordedFixtures.Read("suggestions/issue-picker.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, picker));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-6", []);

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 4, source, "login", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.NotEmpty(loaded.Items);
        Assert.DoesNotContain(loaded.Items, row => row.Detail == "GCM-6");
    }

    [Fact]
    public async Task GivenACreateFormUserQuery_WhenLookedUp_ThenProjectAssignableUsersAreAsked()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));
        var source = new SuggestionSource(SuggestionKind.AssignableUsers, null, [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 5, source, "jo", TimeSpan.FromMilliseconds(250)));

        Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        var request = Assert.Single(fixture.Http.Requests);
        Assert.EndsWith("/rest/api/3/user/assignable/search", request.AbsolutePath);
        Assert.Contains("project=GCM", request.Query);
        Assert.DoesNotContain("issueKey", request.Query);
    }

    [Fact]
    public async Task GivenACreateFormPersonQuery_WhenLookedUp_ThenUsersWhoCanSeeTheProjectAreAsked()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));
        var source = new SuggestionSource(SuggestionKind.ProjectUsers, null, [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 5, source, "jo", TimeSpan.FromMilliseconds(250)));

        Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        var request = Assert.Single(fixture.Http.Requests);
        Assert.EndsWith("/rest/api/3/user/viewissue/search", request.AbsolutePath);
        Assert.Contains("projectKey=GCM", request.Query);
    }

    [Theory]
    [InlineData(SuggestionKind.Components, "components")]
    [InlineData(SuggestionKind.Versions, "versions")]
    public async Task GivenACreateFormProjectListQuery_WhenLookedUp_ThenTheProjectsListIsRead(
        SuggestionKind kind,
        string segment)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, """[{"id":"1","name":"Alpha"}]"""));
        var source = new SuggestionSource(kind, null, [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 5, source, "al", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal("Alpha", Assert.Single(loaded.Items).Label);
        Assert.EndsWith($"/rest/api/3/project/GCM/{segment}", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenACreateFormParentQuery_WhenLookedUp_ThenThePickerIsScopedToTheProject()
    {
        var picker = RecordedFixtures.Read("suggestions/issue-picker.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, picker));
        var source = new SuggestionSource(SuggestionKind.Issues, null, [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 6, source, "login", TimeSpan.FromMilliseconds(250)));

        var request = fixture.Http.Requests.First();
        Assert.Contains("currentJQL=" + Uri.EscapeDataString(JqlBuilder.Project("GCM")), request.Query);
    }

    [Fact]
    public async Task GivenACreateFormParentQuery_WhenThePickerReturnsOtherProjects_ThenOnlyTheProjectsIssuesAreOffered()
    {
        const string mixed = "{\"sections\":[{\"id\":\"cs\",\"label\":\"Current Search\",\"issues\":[{\"id\":1,\"key\":\"GCM-6\",\"summaryText\":\"In\"}]},"
            + "{\"id\":\"hs\",\"label\":\"History Search\",\"issues\":[{\"id\":2,\"key\":\"OTHER-2\",\"summaryText\":\"Out\"}]}]}";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, mixed));
        var source = new SuggestionSource(SuggestionKind.Issues, null, [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 7, source, "i", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["GCM-6"], loaded.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GivenAnEditFormParentQuery_WhenThePickerReturnsOtherProjects_ThenOnlyOtherIssuesOfTheProjectAreOffered()
    {
        const string mixed = "{\"sections\":[{\"id\":\"cs\",\"label\":\"Current Search\",\"issues\":["
            + "{\"id\":1,\"key\":\"GCM-6\",\"summaryText\":\"In\"},{\"id\":3,\"key\":\"GCM-7\",\"summaryText\":\"Self\"}]},"
            + "{\"id\":\"hs\",\"label\":\"History Search\",\"issues\":[{\"id\":2,\"key\":\"OTHER-2\",\"summaryText\":\"Out\"}]}]}";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, mixed));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-7", [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 7, source, "i", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["GCM-6"], loaded.Items.Select(item => item.Id));
        Assert.Contains(Uri.EscapeDataString(JqlBuilder.Project("GCM")), fixture.Http.Requests.First().Query);
    }

    private const string PickerOfThree = "{\"sections\":[{\"id\":\"cs\",\"label\":\"Current Search\",\"issues\":["
        + "{\"id\":1,\"key\":\"GCM-1\",\"summaryText\":\"An epic\"},{\"id\":2,\"key\":\"GCM-2\",\"summaryText\":\"A story\"},"
        + "{\"id\":3,\"key\":\"GCM-3\",\"summaryText\":\"A sub-task\"}]}]}";

    // Company-managed: the types are site-wide and carry no scope. Team-managed: the same levels, scoped to the project.
    private const string CompanyManagedTypes = "{\"issues\":["
        + "{\"key\":\"GCM-1\",\"fields\":{\"issuetype\":{\"name\":\"Epic\",\"hierarchyLevel\":1,\"subtask\":false}}},"
        + "{\"key\":\"GCM-2\",\"fields\":{\"issuetype\":{\"name\":\"Story\",\"hierarchyLevel\":0,\"subtask\":false}}},"
        + "{\"key\":\"GCM-3\",\"fields\":{\"issuetype\":{\"name\":\"Sub-task\",\"hierarchyLevel\":-1,\"subtask\":true}}}]}";

    private const string TeamManagedTypes = "{\"issues\":["
        + "{\"key\":\"GCM-1\",\"fields\":{\"issuetype\":{\"name\":\"Epic\",\"hierarchyLevel\":1,\"subtask\":false,\"scope\":{\"type\":\"PROJECT\",\"project\":{\"id\":\"10001\"}}}}},"
        + "{\"key\":\"GCM-2\",\"fields\":{\"issuetype\":{\"name\":\"Task\",\"hierarchyLevel\":0,\"subtask\":false,\"scope\":{\"type\":\"PROJECT\",\"project\":{\"id\":\"10001\"}}}}},"
        + "{\"key\":\"GCM-3\",\"fields\":{\"issuetype\":{\"name\":\"Subtask\",\"hierarchyLevel\":-1,\"subtask\":true,\"scope\":{\"type\":\"PROJECT\",\"project\":{\"id\":\"10001\"}}}}}]}";

    [Theory]
    [InlineData(CompanyManagedTypes, 1, "GCM-1")]
    [InlineData(CompanyManagedTypes, 0, "GCM-2")]
    [InlineData(TeamManagedTypes, 1, "GCM-1")]
    [InlineData(TeamManagedTypes, 0, "GCM-2")]
    public async Task GivenAnAcceptedParentLevel_WhenTheParentIsLookedUp_ThenOnlyIssuesOfThatLevelAreOffered(
        string types,
        int level,
        string expected)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PickerOfThree).Reply(HttpStatusCode.OK, types));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-9", [], "GCM", level);

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 8, source, "i", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal([expected], loaded.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData(1, "issuetype = \"Epic\"")]
    [InlineData(0, "issuetype in standardIssueTypes()")]
    public async Task GivenAnAcceptedParentLevel_WhenTheParentIsLookedUp_ThenThePickerSearchIsNarrowedByType(
        int level,
        string expected)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PickerOfThree));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-9", [], "GCM", level);

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 8, source, "i", TimeSpan.FromMilliseconds(250)));

        var query = Uri.UnescapeDataString(fixture.Http.Requests.First().Query);
        Assert.Contains(expected, query);
        Assert.Contains("project = \"GCM\"", query);
    }

    [Fact]
    public async Task GivenAnAcceptedParentLevel_WhenTheTypeSearchFails_ThenTheRowsStayForJiraToJudge()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.OK, PickerOfThree).Reply(HttpStatusCode.InternalServerError, "{}"));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-9", [], "GCM", 1);

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 8, source, "i", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["GCM-1", "GCM-2", "GCM-3"], loaded.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GivenNoParentLevel_WhenTheParentIsLookedUp_ThenNothingIsNarrowedByType()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.OK, PickerOfThree).Reply(HttpStatusCode.OK, CompanyManagedTypes));
        var source = new SuggestionSource(SuggestionKind.Issues, "GCM-9", [], "GCM");

        await RunAsync(fixture, new SearchSuggestionsEffect(_tab, 8, source, "i", TimeSpan.FromMilliseconds(250)));

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(3, loaded.Items.Count);
    }

    private static async Task RunAsync(Fixture fixture, SearchSuggestionsEffect effect)
    {
        var task = fixture.Handler.StartAsync(effect);
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;
    }

    private static void TwoSiteReads(ScriptedJiraHandler handler)
    {
        handler.Reply(HttpStatusCode.OK, SiteLabels).Reply(HttpStatusCode.OK, SiteLabels);
    }

    private static async Task LookUpLabelsAsync(Fixture fixture, long requestId, string query)
    {
        var source = new SuggestionSource(SuggestionKind.Labels, null, []);
        var effect = new SearchSuggestionsEffect(_tab, requestId, source, query, TimeSpan.FromMilliseconds(250));

        var task = fixture.Handler.StartAsync(effect);
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;
    }

    private static SearchSuggestionsEffect Effect(long requestId, string query)
    {
        return new SearchSuggestionsEffect(_tab, requestId, _assignee, query, TimeSpan.FromMilliseconds(250));
    }

    private static string Users()
    {
        return RecordedFixtures.Read("suggestions/recorded-users-assignable-gcm.json");
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);
        await store.SetAsync(_other.CredentialKey, "token-b", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };
        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_profile, 1, CancellationToken.None);

        return new Fixture(connections, http);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http)
        {
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
            Handler = new SuggestionHandler(Runner, Time);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public ManualTimeProvider Time { get; } = new();

        public SuggestionHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        /// <summary>Waits (bounded, no fixed sleep) until the handler has armed <paramref name="count"/> timers.</summary>
        public async Task WaitForTimersAsync(int count)
        {
            await WaitUntilAsync(() => Time.PendingTimers >= count);
        }

        public async Task WaitForRequestsAsync(int count)
        {
            await WaitUntilAsync(() => Http.Requests.Count >= count);
        }

        public async Task WaitForPostedAsync(int count)
        {
            await WaitUntilAsync(() => Posted.Count >= count);
        }

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!condition())
            {
                await Task.Delay(1, limit.Token);
            }
        }
    }
}
