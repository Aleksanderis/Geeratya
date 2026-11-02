using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class CommentWriteHandlerTests
{
    private const string Stored =
        """{"id":"500","author":{"accountId":"acc-me","displayName":"Me"},"body":{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Looks good"}]}]},"created":"2026-11-02T10:00:00.000+0000","updated":"2026-11-02T10:00:00.000+0000"}""";

    private const string Filler =
        """{"id":"1","author":{"accountId":"acc-x","displayName":"X"},"body":{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Other"}]}]},"created":"2026-11-01T10:00:00.000+0000","updated":"2026-11-01T10:00:00.000+0000"}""";

    private const string Refused = """{"errorMessages":["You can only edit your own comments"],"errors":{}}""";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly AdfDocument _body = AdfMarkdownParser.Parse("Looks good");
    private static readonly AddCommentRequest _add = new("A-1", _body, "acc-me", 100);
    private static readonly EditCommentRequest _edit = new("A-1", "500", _body);
    private static readonly DeleteCommentRequest _delete = new("A-1", "500");

    [Fact]
    public async Task GivenAnAddedComment_WhenExecuting_ThenOnePostIsSentNoSearchFollowsAndTheStoredCommentIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Created, Stored));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _add));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal("500", succeeded.Comment!.Id);
        Assert.Empty(succeeded.Rows);
        Assert.Equal(["POST"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issue/A-1/comment", Assert.Single(fixture.Http.Requests).AbsolutePath);
        Assert.Contains("Looks good", Assert.Single(fixture.Http.Bodies));
    }

    [Fact]
    public async Task GivenAnEditedComment_WhenExecuting_ThenOnePutIsSentToThatComment()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Stored));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _edit));

        Assert.Equal("500", Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted)).Comment!.Id);
        Assert.Equal(["PUT"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issue/A-1/comment/500", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenADeletedComment_WhenExecuting_ThenOneDeleteIsSentAndNoCommentIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _delete));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Null(succeeded.Comment);
        Assert.Equal(["DELETE"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issue/A-1/comment/500", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.NotFound, WriteErrorKind.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, WriteErrorKind.Validation)]
    [InlineData(HttpStatusCode.TooManyRequests, WriteErrorKind.RateLimited)]
    public async Task GivenARefusedEdit_WhenExecuting_ThenItFailsDefinitelyWithJirasReason(
        HttpStatusCode status,
        WriteErrorKind kind)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, Refused));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _edit));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(kind, failed.Error.Kind);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenALostReplyOnAnAdd_WhenExecuting_ThenTheOutcomeIsUnknownAndThePostIsNeverRepeated()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.InternalServerError, string.Empty).Reply(HttpStatusCode.Created, Stored),
            3);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _add));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenNoConnection_WhenExecutingACommentWrite_ThenItFailsAsNotSent()
    {
        using var fixture = Disconnected();

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _add));

        Assert.Equal(WriteErrorKind.NotSent, Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted)).Error.Kind);
    }

    [Fact]
    public async Task GivenTheNewCommentIsThere_WhenVerifyingAnAdd_ThenItIsAppliedAndCarriedBack()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _add));

        var verified = Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted));
        Assert.Equal(VerifyOutcome.Applied, verified.Outcome);
        Assert.Equal("500", verified.Comment!.Id);
        Assert.Equal(["GET"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenAnEarlierIdenticalComment_WhenVerifyingAnAdd_ThenItIsNotTakenForTheNewOne()
    {
        var older = new AddCommentRequest("A-1", _body, "acc-me", 500);
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, older));

        Assert.Equal(VerifyOutcome.NotApplied, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenACommentOfAnotherAuthor_WhenVerifyingAnAdd_ThenItIsNotTakenForTheNewOne()
    {
        var foreign = new AddCommentRequest("A-1", _body, "acc-other", 100);
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, foreign));

        Assert.Equal(VerifyOutcome.NotApplied, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenNoNewComment_WhenVerifyingAnAdd_ThenItIsNotApplied()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(string.Empty, 0, true)));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _add));

        var verified = Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted));
        Assert.Equal(VerifyOutcome.NotApplied, verified.Outcome);
        Assert.Null(verified.Comment);
    }

    [Fact]
    public async Task GivenTheCommentIsOnALaterPage_WhenVerifyingAnAdd_ThenTheNextPageIsReadToo()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, Page(Filler, 1, false, 150))
            .Reply(HttpStatusCode.OK, Page(Stored, 1, true, 150, 149)));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _add));

        Assert.Equal(VerifyOutcome.Applied, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    [Fact]
    public async Task GivenTheCheckCannotRead_WhenVerifyingAComment_ThenItIsUnreachable()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, string.Empty));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _add));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenTheEditedTextIsStored_WhenVerifyingAnEdit_ThenItIsAppliedElseNotApplied()
    {
        var changed = new EditCommentRequest("A-1", "500", AdfMarkdownParser.Parse("Something else"));
        using var applied = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));
        using var notApplied = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));

        await applied.Handler.StartAsync(new VerifyWriteEffect(41, _edit));
        await notApplied.Handler.StartAsync(new VerifyWriteEffect(41, changed));

        Assert.Equal(VerifyOutcome.Applied, Assert.IsType<WriteVerified>(Assert.Single(applied.Posted)).Outcome);
        Assert.Equal(VerifyOutcome.NotApplied, Assert.IsType<WriteVerified>(Assert.Single(notApplied.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenTheCommentIsGone_WhenVerifyingADelete_ThenItIsAppliedAndWhenStillThereNotApplied()
    {
        using var gone = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(string.Empty, 0, true)));
        using var there = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Page(Stored, 1, true)));

        await gone.Handler.StartAsync(new VerifyWriteEffect(41, _delete));
        await there.Handler.StartAsync(new VerifyWriteEffect(41, _delete));

        Assert.Equal(VerifyOutcome.Applied, Assert.IsType<WriteVerified>(Assert.Single(gone.Posted)).Outcome);
        Assert.Equal(VerifyOutcome.NotApplied, Assert.IsType<WriteVerified>(Assert.Single(there.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenSavingAComment_ThenTheEditorClosesAndTheStoredCommentShows()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Created, Stored));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenIssue(reducer);

        var editing = Press(reducer, state, "Space", "m", "a").State;
        var typed = reducer.Reduce(editing, new TextPasted("Looks good")).State;
        var saving = reducer.Reduce(typed, new KeyPressed("Ctrl-s"));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saving.Effects)));
        var done = reducer.Reduce(saving.State, Assert.Single(fixture.Posted));

        Assert.Null(done.State.Editor);
        Assert.Equal(InputMode.Normal, done.State.Mode);
        Assert.Equal("Added a comment to A-1", done.State.Message);
        var comment = Assert.Single(done.State.Session.Tabs[0].Detail[0].Comments);
        Assert.Equal("500", comment.Id);
        Assert.Contains("Looks good", AdfMarkdownWriter.ToText(comment.Body));
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenAMentionAndAnIssueLinkChosenInThePopups_WhenSaving_ThenJiraGetsAMentionNodeAndALinkMarkAndTheDetailShowsBoth()
    {
        const string stored =
            """{"id":"501","author":{"accountId":"acc-me","displayName":"Me"},"body":{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"cc "},{"type":"mention","attrs":{"id":"acc-olga","text":"@Olga Other"}},{"type":"text","text":" see "},{"type":"text","text":"GCM-12","marks":[{"type":"link","attrs":{"href":"https://a.atlassian.net/browse/GCM-12"}}]}]}]},"created":"2026-11-02T10:00:00.000+0000","updated":"2026-11-02T10:00:00.000+0000"}""";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Created, stored));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = Press(reducer, OpenIssue(reducer), "Space", "m", "a").State;
        var tab = state.Editor!.Tab;

        state = Press(reducer, state, "c", "c", "Space", "@", "o").State;
        state = reducer.Reduce(state, new SuggestionsLoaded(
            tab,
            state.Editor!.Suggest!.RequestId,
            [new Suggestion("acc-olga", "Olga Other", null, false)])).State;
        state = Press(reducer, state, "Tab", "s", "e", "e", "Space", "#", "G").State;
        state = reducer.Reduce(state, new SuggestionsLoaded(
            tab,
            state.Editor!.Suggest!.RequestId,
            [new Suggestion("GCM-12", "Fix", "GCM-12", false)])).State;
        state = Press(reducer, state, "Tab").State;

        var saving = reducer.Reduce(state, new KeyPressed("Ctrl-s"));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saving.Effects)));
        var done = reducer.Reduce(saving.State, Assert.Single(fixture.Posted));

        using var sent = System.Text.Json.JsonDocument.Parse(Assert.Single(fixture.Http.Bodies));
        var inline = sent.RootElement.GetProperty("body").GetProperty("content")[0].GetProperty("content");
        var mention = inline.EnumerateArray().Single(node => node.GetProperty("type").GetString() == "mention");
        Assert.Equal("acc-olga", mention.GetProperty("attrs").GetProperty("id").GetString());
        var link = inline.EnumerateArray()
            .Single(node => node.TryGetProperty("marks", out var marks) && marks[0].GetProperty("type").GetString() == "link");
        Assert.Equal("GCM-12", link.GetProperty("text").GetString());
        Assert.Equal(
            "https://a.atlassian.net/browse/GCM-12",
            link.GetProperty("marks")[0].GetProperty("attrs").GetProperty("href").GetString());

        var comment = Assert.Single(done.State.Session.Tabs[0].Detail[0].Comments);
        Assert.Contains("@[Olga Other](mention:acc-olga)", AdfMarkdownWriter.ToText(comment.Body));
        var text = string.Join('\n', done.State.Session.Tabs[0].Detail[0].Document!.Lines.Select(line => line.PlainText));
        Assert.Contains("@Olga Other", text);
        Assert.Contains("GCM-12", text);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenJiraRefusesTheComment_ThenTheEditorKeepsTheTextAndShowsTheReason()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Forbidden, Refused));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var typed = reducer.Reduce(
            Press(reducer, OpenIssue(reducer), "Space", "m", "a").State,
            new TextPasted("Looks good")).State;

        var saving = reducer.Reduce(typed, new KeyPressed("Ctrl-s"));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(saving.Effects)));
        var failed = reducer.Reduce(saving.State, Assert.Single(fixture.Posted));

        var editor = Assert.IsType<CommentEditor>(failed.State.Editor);
        Assert.Equal("Looks good", editor.Buffer.Text);
        Assert.Equal("No permission to add a comment to A-1: You can only edit your own comments", editor.Error);
        Assert.False(editor.IsSaving);
        Assert.Empty(failed.State.Session.Operations);
    }

    private static ReduceResult Press(AppReducer reducer, AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private static AppState OpenIssue(AppReducer reducer)
    {
        var profiles = ProfileCatalog.Empty.With(_profile);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(_profile) with { Connection = new Connected("Me", "acc-me") },
        };
        var sized = reducer.Reduce(start, new ViewportChanged(30, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var row = new IssueSummary(
            "1",
            "A-1",
            "One",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
        var loaded = reducer.Reduce(
            opened,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), DateTimeOffset.UnixEpoch)).State;

        var detail = reducer.Reduce(loaded, new KeyPressed("Enter")).State;
        var shown = detail.Session.Tabs[0];
        var empty = new OffsetPage<IssueComment>([], 0, DetailPaging.CommentPageSize, 0, true);

        return reducer.Reduce(
            detail,
            new IssueCommentsLoaded(shown.Id, shown.Detail[0].CommentsRequest, empty, DateTimeOffset.UnixEpoch)).State;
    }

    private static string Page(string comment, int count, bool last, int total = 0, int startAt = 0)
    {
        var shown = total == 0 ? count : total;
        var items = comment.Length == 0 ? string.Empty : comment;

        return $$"""{"startAt":{{startAt}},"maxResults":100,"total":{{shown}},"isLast":{{last.ToString().ToLowerInvariant()}},"comments":[{{items}}]}""";
    }

    private static Fixture Disconnected()
    {
        var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        return new Fixture(connections, new ScriptedJiraHandler());
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script, int maxRetries = 0)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = maxRetries,
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
            Handler = new WriteHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public WriteHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
