using System.Net;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Sprints;

public sealed class BoardKindResolverTests
{
    private const string TeamSprintsPath = "/rest/agile/1.0/board/5/sprint";
    private const string TeamKanbanSprintsPath = "/rest/agile/1.0/board/3/sprint";

    [Theory]
    [InlineData(BoardType.Scrum, BoardKind.Scrum)]
    [InlineData(BoardType.Kanban, BoardKind.Kanban)]
    public async Task GivenCompanyManagedBoardType_WhenResolving_ThenNoRequestIsSent(BoardType type, BoardKind expected)
    {
        using var fixture = new TransportFixture();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(new BoardSummary(6, "b", type, "GCS"), CancellationToken.None);

        Assert.Equal(expected, result.Value);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenTeamManagedScrumBoard_WhenResolving_ThenProbeSucceedsAndBoardIsScrum()
    {
        using var fixture = ServingTeamScrum();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(Board(5), CancellationToken.None);

        Assert.Equal(BoardKind.Scrum, result.Value);
        Assert.Equal(BoardKind.Scrum, resolver.Recall(5));
    }

    [Fact]
    public async Task GivenTeamManagedKanbanBoard_WhenResolving_ThenProbeIs400AndBoardIsKanban()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            TeamKanbanSprintsPath,
            FakeResponse.WithStatus(
                HttpStatusCode.BadRequest,
                FixtureFiles.Read("scrum/error-sprints-gtm-kanban-400.json")));
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(Board(3), CancellationToken.None);

        Assert.Equal(BoardKind.Kanban, result.Value);
        Assert.Equal(BoardKind.Kanban, resolver.Recall(3));
    }

    [Fact]
    public async Task GivenProbedBoard_WhenResolvingAgain_ThenNoSecondRequestIsSent()
    {
        using var fixture = ServingTeamScrum();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        await resolver.ResolveAsync(Board(5), CancellationToken.None);
        var second = await resolver.ResolveAsync(Board(5), CancellationToken.None);

        Assert.Equal(BoardKind.Scrum, second.Value);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenSeededAnswer_WhenResolving_ThenProbeIsSkipped()
    {
        using var fixture = new TransportFixture();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));
        resolver.Remember(3, BoardKind.Kanban);

        var result = await resolver.ResolveAsync(Board(3), CancellationToken.None);

        Assert.Equal(BoardKind.Kanban, result.Value);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    public async Task GivenOtherProbeFailure_WhenResolving_ThenErrorIsReturnedAndNothingIsRemembered(
        HttpStatusCode status,
        JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, TeamSprintsPath, FakeResponse.WithStatus(status, "{}"));
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(Board(5), CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
        Assert.Null(resolver.Recall(5));
    }

    [Fact]
    public async Task GivenBackgroundPriority_WhenResolving_ThenProbeIsSentAsBackgroundWork()
    {
        using var fixture = ServingTeamScrum();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(Board(5), CancellationToken.None, JiraPriority.Background);

        Assert.Equal(BoardKind.Scrum, result.Value);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenUnknownBoardType_WhenResolving_ThenItIsProbedLikeSimple()
    {
        using var fixture = ServingTeamScrum();
        var resolver = new BoardKindResolver(new SprintClient(fixture.Transport));

        var result = await resolver.ResolveAsync(
            new BoardSummary(5, "b", BoardType.Unknown, "GTS"),
            CancellationToken.None);

        Assert.Equal(BoardKind.Scrum, result.Value);
        Assert.Single(fixture.Handler.Requests);
    }

    private static TransportFixture ServingTeamScrum()
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            TeamSprintsPath,
            FakeResponse.Ok(FixtureFiles.Read("scrum/sprints-gts-page1.json")));

        return fixture;
    }

    private static BoardSummary Board(long id) => new(id, "board", BoardType.Simple, "GTS");
}
