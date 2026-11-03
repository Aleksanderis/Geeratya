using System.Net;
using Geeratya.Jira.Fields;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Fields;

public sealed class FieldClientTests
{
    private const string FieldPath = "/rest/api/3/field";

    [Fact]
    public async Task GivenRecordedFieldList_WhenReading_ThenSprintIsFoundBySchemaAndEstimatesByName()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));

        var result = await new FieldClient(fixture.Transport).GetExtraFieldsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("customfield_10020", result.Value.SprintFieldId);
        Assert.Equal(["customfield_10016", "customfield_10047"], result.Value.EstimateFieldIds);
    }

    [Fact]
    public async Task GivenSiteWithoutSoftwareFields_WhenReading_ThenNothingIsResolved()
    {
        const string json = """[{"id":"summary","name":"Summary","schema":{"type":"string"}},{"name":"no id"},{"id":"x"}]""";
        using var fixture = Serving(json);

        var result = await new FieldClient(fixture.Transport).GetExtraFieldsAsync(CancellationToken.None);

        Assert.Null(result.Value.SprintFieldId);
        Assert.Empty(result.Value.EstimateFieldIds);
    }

    [Fact]
    public async Task GivenSprintFieldUnderAnotherId_WhenReading_ThenTheIdComesFromTheSchemaNotFromCode()
    {
        const string json =
            """[{"id":"customfield_99999","name":"Iteration","schema":{"custom":"com.pyxis.greenhopper.jira:gh-sprint"}}]""";
        using var fixture = Serving(json);

        var result = await new FieldClient(fixture.Transport).GetExtraFieldsAsync(CancellationToken.None);

        Assert.Equal("customfield_99999", result.Value.SprintFieldId);
    }

    [Fact]
    public async Task GivenTwoCalls_WhenSiteFieldsAreAsked_ThenOnlyTheFirstSendsARequest()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));
        var fields = new SiteFields(new FieldClient(fixture.Transport));

        await fields.GetAsync(CancellationToken.None);
        var second = await fields.GetAsync(CancellationToken.None);

        Assert.Single(fixture.Handler.Requests);
        Assert.Equal("customfield_10020", second.Value.SprintFieldId);
    }

    [Fact]
    public async Task GivenFailedRead_WhenAskedAgainWithinTheFailureMemory_ThenNoSecondRequestIsSent()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));
        fixture.Handler.Script(HttpMethod.Get, FieldPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden, "{}"));
        var clock = new MovableClock();
        var fields = new SiteFields(new FieldClient(fixture.Transport), clock);

        var failed = await fields.GetAsync(CancellationToken.None);
        clock.Advance(SiteFields.FailureMemory / 2);
        var again = await fields.GetAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.Forbidden, failed.Error.Kind);
        Assert.Equal(JiraErrorKind.Forbidden, again.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenFailedRead_WhenTheFailureMemoryHasPassed_ThenItAsksAgainAndKeepsTheAnswer()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));
        fixture.Handler.Script(HttpMethod.Get, FieldPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden, "{}"));
        var clock = new MovableClock();
        var fields = new SiteFields(new FieldClient(fixture.Transport), clock);

        await fields.GetAsync(CancellationToken.None);
        clock.Advance(SiteFields.FailureMemory + TimeSpan.FromSeconds(1));
        var retried = await fields.GetAsync(CancellationToken.None);
        await fields.GetAsync(CancellationToken.None);

        Assert.True(retried.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenWarmUp_WhenTheIdsAreAsked_ThenTheyShareOneRequest()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));
        var fields = new SiteFields(new FieldClient(fixture.Transport));

        fields.Warm();
        var asked = await fields.GetAsync(CancellationToken.None);
        await fields.GetAsync(CancellationToken.None);

        Assert.True(asked.IsSuccess);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenBoardEstimationField_WhenAskingForTheBoard_ThenItComesBeforeTheSiteCandidates()
    {
        using var fixture = Serving(FixtureFiles.Read("metadata/field-list.json"));
        var fields = new SiteFields(new FieldClient(fixture.Transport));

        fields.RememberEstimate(6, "customfield_10047");
        var board = await fields.GetForBoardAsync(6, CancellationToken.None);
        var other = await fields.GetForBoardAsync(5, CancellationToken.None);

        Assert.Equal("customfield_10047", board.Value.EstimateFieldIds[0]);
        Assert.Equal("customfield_10016", other.Value.EstimateFieldIds[0]);
    }

    private static TransportFixture Serving(string json)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, FieldPath, FakeResponse.Ok(json));

        return fixture;
    }
}

file sealed class MovableClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}
