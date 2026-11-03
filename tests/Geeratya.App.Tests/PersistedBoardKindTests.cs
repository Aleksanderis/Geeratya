using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.BoardKinds;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

public sealed class PersistedBoardKindTests : IDisposable
{
    private const string SprintPage = """{"maxResults":1,"startAt":0,"isLast":true,"values":[]}""";
    private const string NoSprints = """{"errorMessages":["The board does not support sprints"],"errors":{}}""";

    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");
    private static readonly BoardSummary _board = new(7, "Team", BoardType.Simple, "TEAM");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-kinds-{Guid.NewGuid():N}");

    [Fact]
    public async Task GivenProbedBoard_WhenTheAppRestarts_ThenTheBoardIsNotProbedAgain()
    {
        var first = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, SprintPage);
        var second = new ScriptedJiraHandler();

        Assert.Equal(BoardKind.Scrum, await ResolveAsync(first, _work));
        Assert.Equal(BoardKind.Scrum, await ResolveAsync(second, _work));

        Assert.Single(first.Requests);
        Assert.Empty(second.Requests);
    }

    [Fact]
    public async Task GivenKanbanAnswerOfOneProfile_WhenAnotherProfileResolves_ThenItProbesAndKeepsItsOwnAnswer()
    {
        var workHandler = new ScriptedJiraHandler().Reply(HttpStatusCode.BadRequest, NoSprints);
        var devHandler = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, SprintPage);

        Assert.Equal(BoardKind.Kanban, await ResolveAsync(workHandler, _work));
        Assert.Equal(BoardKind.Scrum, await ResolveAsync(devHandler, _dev));

        var idle = new ScriptedJiraHandler();

        Assert.Equal(BoardKind.Kanban, await ResolveAsync(idle, _work));
        Assert.Equal(BoardKind.Scrum, await ResolveAsync(idle, _dev));
        Assert.Single(devHandler.Requests);
        Assert.Empty(idle.Requests);
    }

    [Fact]
    public async Task GivenFailedProbe_WhenTheAppRestarts_ThenNothingWasStoredAndItProbesAgain()
    {
        var failing = new ScriptedJiraHandler().Reply(HttpStatusCode.Forbidden, "{}");
        var next = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, SprintPage);

        Assert.Null(await ResolveAsync(failing, _work));
        Assert.Equal(BoardKind.Scrum, await ResolveAsync(next, _work));
        Assert.Single(next.Requests);
    }

    [Fact]
    public async Task GivenProfileRemoved_WhenDeletingItsAnswers_ThenTheyAreGone()
    {
        await ResolveAsync(new ScriptedJiraHandler().Reply(HttpStatusCode.OK, SprintPage), _work);

        var store = new BoardKindStore(_directory);

        Assert.NotEmpty(store.Load(_work));

        store.Delete(_work);

        Assert.Empty(store.Load(_work));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<BoardKind?> ResolveAsync(ScriptedJiraHandler handler, Profile profile)
    {
        var secrets = new FakeSecretStore();

        await secrets.SetAsync(profile.CredentialKey, "token", CancellationToken.None);

        using var connections = new SiteConnections(
            new TokenProvider(secrets, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(handler),
            new BoardKindStore(_directory));

        await connections.ActivateAsync(profile, profileCount: 2, CancellationToken.None);

        var result = await JiraServices.Create(connections)!.BoardKinds.ResolveAsync(_board, CancellationToken.None);

        return result.IsSuccess ? result.Value : null;
    }
}
