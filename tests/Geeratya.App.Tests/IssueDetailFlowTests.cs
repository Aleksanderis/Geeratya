using System.Collections.Concurrent;
using System.Net;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

/// <summary>
/// The issue detail through the real path: Enter in the reducer, the load handler against the recorded fixtures
/// served by an in-process Jira, and the replies back through the reducer.
/// </summary>
public sealed class IssueDetailFlowTests : IDisposable
{
    private const string Site = "/rest/api/3/issue";
    private const string SearchPath = "/rest/api/3/search/jql";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());
    private readonly RoutedJiraHandler _http = new();
    private readonly ConcurrentQueue<Command> _posted = new();
    private SiteConnections? _connections;
    private NetworkRunner? _runner;
    private IssueDetailLoadHandler? _handler;
    private readonly List<string> _folders = [];
    private SiteCache? _cache;

    public void Dispose()
    {
        _runner?.Dispose();
        _connections?.Dispose();
        _cache?.Dispose();

        foreach (var folder in _folders.Where(Directory.Exists))
        {
            Directory.Delete(folder, true);
        }
    }

    private string CacheFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"geeratya-detail-{Guid.NewGuid():N}");

        _folders.Add(folder);

        return folder;
    }

    private static IssueDetail Sample()
    {
        return new IssueDetail(
            "1",
            "GTM-5",
            "Sample",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            null,
            [],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "GTM",
            null,
            null,
            [],
            [],
            []);
    }

    [Fact]
    public async Task GivenTeamManagedIssueWithRichAdf_WhenOpened_ThenDescriptionAndCommentsAreFormattedAndTheFieldListIsReadOnce()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json")
            .Fixture($"{Site}/GTM-5/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GTM-5/remotelink", "links/remotelink-list-gtm5.json")
            .Fixture("/rest/api/3/field", "metadata/field-list.json");
        await ConnectAsync();

        var state = await OpenAsync("GTM-5");

        var detail = Top(state);
        var text = Text(detail);
        Assert.Contains("Heading one", text);
        Assert.Contains("Survey4 task with rich ADF", text);
        Assert.Contains("Comments 2", text);
        Assert.Contains("Test User 1", text);
        Assert.Equal(LoadState.Idle, detail.IssueLoad);
        Assert.Equal(LoadState.Idle, detail.CommentsLoad);
        Assert.Equal(LoadState.Idle, detail.LinksLoad);
        Assert.NotNull(detail.RemoteLinks);
        Assert.Equal(
            [
                "/rest/api/3/field",
                $"{Site}/GTM-5",
                $"{Site}/GTM-5/comment",
                $"{Site}/GTM-5/editmeta",
                $"{Site}/GTM-5/remotelink",
            ],
            _http.Requests.Select(uri => uri.AbsolutePath).Order());
    }

    [Fact]
    public async Task GivenFailingFieldList_WhenTwoIssuesAreOpened_ThenTheIssuesLoadAndTheFieldListIsAskedOnce()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json")
            .Fixture($"{Site}/GTM-5/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GTM-5/remotelink", "links/remotelink-list-gtm5.json")
            .Fixture("/rest/api/3/field", "metadata/field-list.json", HttpStatusCode.Forbidden);
        await ConnectAsync();

        var first = await OpenAsync("GTM-5");
        var second = await OpenAsync("GTM-5");

        Assert.NotNull(Top(first).Issue);
        Assert.NotNull(Top(second).Issue);
        Assert.Equal(1, _http.Requests.Count(uri => uri.AbsolutePath == "/rest/api/3/field"));
    }

    [Fact]
    public async Task GivenWarmedFieldList_WhenAnIssueIsOpened_ThenNoSecondFieldRequestIsSent()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json")
            .Fixture($"{Site}/GTM-5/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GTM-5/remotelink", "links/remotelink-list-gtm5.json")
            .Fixture("/rest/api/3/field", "metadata/field-list.json");
        await ConnectAsync();

        _connections!.WarmFields();
        _connections.WarmFields();
        var state = await OpenAsync("GTM-5");

        Assert.NotNull(Top(state).Issue);
        Assert.Equal(1, _http.Requests.Count(uri => uri.AbsolutePath == "/rest/api/3/field"));
    }

    [Fact]
    public async Task GivenScrumIssue_WhenOpened_ThenDetailCarriesSprintsEstimateAndEpicFromTheSiteFields()
    {
        _http.Fixture($"{Site}/GCS-8", "scrum/issue-gcs-carried-over.json")
            .Fixture($"{Site}/GCS-8/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GCS-8/remotelink", "links/remotelink-list-gtm5.json")
            .Fixture("/rest/api/3/field", "metadata/field-list.json");
        await ConnectAsync();

        var state = await OpenAsync("GCS-8");

        var issue = Top(state).Issue!;
        Assert.Equal(["Seed Sprint 1", "Seed Sprint 2"], issue.Sprints.Select(sprint => sprint.Name));
        Assert.Equal("Seed Sprint 2", issue.CurrentSprint?.Name);
        Assert.NotNull(issue.Estimate);
        Assert.Equal("GCS-2", issue.Epic?.Key);
        Assert.Contains(_http.Requests, uri => uri.Query.Contains("customfield_10020", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenCachedIssue_WhenOpened_ThenTheCachedIssueIsPostedBeforeTheNetworkReply()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json")
            .Fixture($"{Site}/GTM-5/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GTM-5/remotelink", "links/remotelink-list-gtm5.json");
        await ConnectAsync();
        var cache = new SiteCache(CacheFolder());
        _cache = cache;
        var fresh = await OpenAsync("GTM-5");
        var issue = Top(fresh).Issue!;
        cache.PutIssue(new CachedIssue(issue with { Summary = "Cached summary" }, DateTimeOffset.UnixEpoch));
        _handler = new IssueDetailLoadHandler(_runner!);

        var state = ListState("GTM-5");
        var opened = _reducer.Reduce(state, new KeyPressed("Enter"));
        var effect = opened.Effects.OfType<LoadIssueDetailEffect>().Single();
        _posted.Clear();

        await _handler.StartAsync(effect, cache);

        var posted = _posted.ToArray();
        var cached = Assert.IsType<IssueDetailCached>(posted[0]);
        Assert.IsType<IssueDetailLoaded>(posted[1]);
        Assert.Equal("Cached summary", cached.Cached.Detail.Summary);

        var shown = _reducer.Reduce(opened.State, cached).State;
        Assert.True(Top(shown).IssueStale);
        Assert.Contains("Cached summary", Text(Top(shown)));
    }

    [Fact]
    public async Task GivenEmptyCache_WhenOpened_ThenOnlyTheNetworkReplyIsPosted()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json");
        await ConnectAsync();
        _cache = new SiteCache(CacheFolder());
        var opened = _reducer.Reduce(ListState("GTM-5"), new KeyPressed("Enter"));
        var effect = opened.Effects.OfType<LoadIssueDetailEffect>().Single();
        _posted.Clear();

        await _handler!.StartAsync(effect, _cache);

        Assert.IsType<IssueDetailLoaded>(Assert.Single(_posted));
    }

    [Fact]
    public async Task GivenCachedIssueAndNoConnection_WhenOpened_ThenTheCachedIssueIsPostedBeforeTheFailure()
    {
        _runner = new NetworkRunner(new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)),
            new FakeTransportFactory(_http)), new QueueCommandSink(_posted), CancellationToken.None);
        _handler = new IssueDetailLoadHandler(_runner);
        _cache = new SiteCache(CacheFolder());
        var opened = _reducer.Reduce(ListState("GTM-5"), new KeyPressed("Enter"));
        var effect = opened.Effects.OfType<LoadIssueDetailEffect>().Single();
        _cache.PutIssue(new CachedIssue(Sample(), DateTimeOffset.UnixEpoch));
        _posted.Clear();

        await _handler.StartAsync(effect, _cache);

        Assert.IsType<IssueDetailCached>(_posted.ToArray()[0]);
        Assert.IsType<IssueDetailFailed>(_posted.ToArray()[1]);
    }

    [Theory]
    [InlineData("GCM-6", "issues/issue-gcm6.json", "comments/comments-gcm6.json", "metadata/editmeta-gcm-26.json")]
    [InlineData(
        "GTM-5",
        "adf/issue-gtm5-rich-description.json",
        "adf/comments-gtm5.json",
        "metadata/editmeta-gtm-11.json")]
    public async Task GivenEitherProjectKind_WhenOpened_ThenTheEditMetadataDecidesWhichDetailsFieldsAreMarkedEditable(
        string key,
        string issueFixture,
        string commentsFixture,
        string editmetaFixture)
    {
        _http.Fixture($"{Site}/{key}", issueFixture)
            .Fixture($"{Site}/{key}/comment", commentsFixture)
            .Fixture($"{Site}/{key}/remotelink", "links/remotelink-list-empty.json")
            .Fixture($"{Site}/{key}/editmeta", editmetaFixture)
            .Fixture("/rest/api/3/field", "metadata/field-list.json");
        await ConnectAsync();

        var state = await OpenAsync(key);

        var detail = Top(state);
        Assert.Contains(DetailFieldIds.Priority, detail.EditableFields!);
        Assert.Contains(DetailFieldIds.Labels, detail.EditableFields!);
        Assert.Contains(DetailFieldIds.Assignee, detail.EditableFields!);
        Assert.True(detail.Document!.Side!.Lines.Count(line => line.PlainText.Contains('▾')) >= 4);
        Assert.Contains(_http.Requests, uri => uri.AbsolutePath == $"{Site}/{key}/editmeta");
    }

    [Theory]
    [InlineData("GCM-10", "gcm10", "metadata/editmeta-gcm-26.json")]
    [InlineData("GTM-11", "gtm11", "metadata/editmeta-gtm-11.json")]
    public async Task GivenEitherProjectKind_WhenOpeningThePriorityDropdown_ThenTheRecordedEditMetadataFillsThePopup(
        string key,
        string fixture,
        string editmetaFixture)
    {
        _http.Fixture($"{Site}/{key}", $"issues/issue-{fixture}.json")
            .Fixture($"{Site}/{key}/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/{key}/remotelink", "links/remotelink-list-empty.json")
            .Fixture($"{Site}/{key}/editmeta", editmetaFixture);
        await ConnectAsync();
        var state = await OpenAsync(key);

        foreach (var pressed in new[] { "Ctrl-l", "j" })
        {
            state = _reducer.Reduce(state, new KeyPressed(pressed)).State;
        }

        var opened = await PumpAsync(state, new KeyPressed("Enter"));

        var form = Tab(opened).Form!;
        Assert.Equal(DetailFieldIds.Priority, form.Dropdown);
        Assert.True(form.Fields.Count > 0, form.TopError);
        Assert.Equal(DetailFieldIds.Priority, Assert.Single(form.Fields).Id);
        Assert.NotEmpty(form.Suggest!.Items);
    }

    [Fact]
    public async Task GivenEditMetadataFails_WhenOpened_ThenTheDetailStillShowsAndOnlyStatusIsMarked()
    {
        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json")
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json")
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json")
            .Fixture($"{Site}/GCM-6/editmeta", "errors/issue-404.json", HttpStatusCode.NotFound);
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        var detail = Top(state);
        Assert.Null(detail.EditableFields);
        Assert.Equal(LoadState.Idle, detail.IssueLoad);
        Assert.Equal(["▾"], detail.Document!.Side!.Lines.Where(line => line.PlainText.Contains('▾')).Select(_ => "▾"));
    }

    [Fact]
    public async Task GivenCompanyManagedIssueWithComments_WhenOpened_ThenTheNoDescriptionTextAndAllCommentsShow()
    {
        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json")
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json")
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json");
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        var text = Text(Top(state));
        Assert.Contains("No description.", text);
        Assert.Contains("Parent", text);
        Assert.Contains(" GCM-1", text);
        Assert.Contains("Comments 3", text);
        Assert.Contains("Written by an inactive account", text);
        Assert.Contains("żółć 日本語", text);
    }

    [Fact]
    public async Task GivenCompanyManagedIssueWithMediaDescription_WhenOpened_ThenMediaShowsAsAMarkerNotAnImage()
    {
        _http.Fixture($"{Site}/GCM-24", "issues/issue-gcm24.json")
            .Fixture($"{Site}/GCM-24/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/GCM-24/remotelink", "links/remotelink-list-empty.json");
        await ConnectAsync();

        var state = await OpenAsync("GCM-24");

        var text = Text(Top(state));
        Assert.Contains("[image]", text);
        Assert.Contains("No comments.", text);
    }

    [Theory]
    [InlineData("GTM-11", "gtm11", "links/remotelink-list-gtm5.json", 1, "Remote page", "GTM-9", "GTM-19", "GTM-12")]
    [InlineData(
        "GCM-10",
        "gcm10",
        "links/remotelink-list-gcm6.json",
        3,
        "Design notes · Confluence",
        "GCM-8",
        "GCM-18",
        "GCM-11")]
    public async Task GivenStoryOfEitherProjectKind_WhenOpened_ThenSubtasksLinksRemoteLinksAndAttachmentsShow(
        string key,
        string fixture,
        string linksFile,
        int linkCount,
        string linkTitle,
        string parent,
        string firstSubtask,
        string blockedBy)
    {
        _http.Fixture($"{Site}/{key}", $"issues/issue-{fixture}.json")
            .Fixture($"{Site}/{key}/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/{key}/remotelink", linksFile)
            .Fixture(SearchPath, "hierarchy/search-epic-children-gtm.json");
        await ConnectAsync();

        var state = await OpenAsync(key);

        var text = Text(Top(state));
        Assert.Contains("Epic", text);
        Assert.Contains($" {parent}", text);
        Assert.Contains("Sub-tasks 2", text);
        Assert.Contains(firstSubtask, text);
        Assert.Contains("Linked issues 2", text);
        Assert.Contains($"{blockedBy}  is blocked by ", text);
        Assert.Contains("relates to ", text);
        Assert.Contains($"Remote links {linkCount}", text);
        Assert.Contains(linkTitle, text);
        Assert.Contains("Attachments 1", text);
        Assert.Contains("seed-notes.txt · ", text);
        Assert.Contains(" · 47 B", text);
        Assert.DoesNotContain("Child issues", text);

        // Jira lists sub-tasks without an assignee: the one children search (parent = KEY) supplies the names.
        var search = Assert.Single(_http.Requests, uri => uri.AbsolutePath == SearchPath);
        Assert.Contains(Uri.EscapeDataString($"parent = {key}"), search.Query);
    }

    [Theory]
    [InlineData("GTM-9", "gtm9", "hierarchy/search-epic-children-gtm.json", "GTM-7", "GTM-6")]
    [InlineData("GCM-8", "gcm8", "hierarchy/search-epic-children-gcm.json", "GCM-7", "GCM-1")]
    public async Task GivenEpicOfEitherProjectKind_WhenOpened_ThenItsChildrenAreSearchedByParentAndShown(
        string key,
        string issue,
        string childrenFixture,
        string firstChild,
        string secondChild)
    {
        _http.Fixture($"{Site}/{key}", $"issues/issue-{issue}.json")
            .Fixture($"{Site}/{key}/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/{key}/remotelink", "links/remotelink-list-empty.json")
            .Fixture(SearchPath, childrenFixture);
        await ConnectAsync();

        var state = await OpenAsync(key);

        var detail = Top(state);
        var text = Text(detail);
        Assert.Contains("Child issues 2", text);
        Assert.Contains(firstChild, text);
        Assert.Contains(secondChild, text);
        Assert.DoesNotContain("Remote links", text);
        Assert.Equal(LoadState.Idle, detail.ChildrenLoad);

        var search = Assert.Single(_http.Requests, uri => uri.AbsolutePath == SearchPath);
        Assert.Contains(Uri.EscapeDataString($"parent = {key}"), search.Query);
    }

    [Fact]
    public async Task GivenAnExpandedBoardCard_WhenItsSubtaskAssigneesAreLoaded_ThenTheParentSearchAnswersPerKey()
    {
        _http.Fixture(SearchPath, "hierarchy/search-epic-children-gtm.json");
        await ConnectAsync();

        await _handler!.StartAsync(new LoadSubtaskAssigneesEffect(new TabId(1), "GTM-9"));

        var loaded = Assert.IsType<SubtaskAssigneesLoaded>(Assert.Single(_posted));
        Assert.Equal("GTM-9", loaded.ParentKey);
        Assert.Equal(["GTM-7", "GTM-6"], loaded.Assignees.Select(each => each.Key));

        var search = Assert.Single(_http.Requests, uri => uri.AbsolutePath == SearchPath);
        Assert.Contains(Uri.EscapeDataString("parent = GTM-9"), search.Query);
    }

    [Fact]
    public async Task GivenTheSubtaskSearchIsRefused_WhenAssigneesAreLoaded_ThenNothingIsPosted()
    {
        _http.Fixture(SearchPath, "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized);
        await ConnectAsync();

        await _handler!.StartAsync(new LoadSubtaskAssigneesEffect(new TabId(1), "GTM-9"));

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task GivenChildrenSearchRefused_WhenEpicOpened_ThenOnlyTheChildrenSectionShowsTheError()
    {
        _http.Fixture($"{Site}/GTM-9", "issues/issue-gtm9.json")
            .Fixture($"{Site}/GTM-9/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/GTM-9/remotelink", "links/remotelink-list-empty.json")
            .Fixture(SearchPath, "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized);
        await ConnectAsync();

        var state = await OpenAsync("GTM-9");

        var detail = Top(state);
        Assert.IsType<Failed>(detail.ChildrenLoad);
        Assert.Contains("Child issues", Text(detail));
        Assert.Contains("Check the token", Text(detail));
        Assert.Contains("No comments.", Text(detail));
        Assert.NotNull(detail.Issue);
    }

    [Fact]
    public async Task GivenIssueNotFound_WhenOpened_ThenTheSameNotFoundTextShowsAndTheListIsStillUnderneath()
    {
        _http.Fixture($"{Site}/GCM-99", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/comment", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/remotelink", "errors/issue-404.json", HttpStatusCode.NotFound);
        await ConnectAsync();

        var state = await OpenAsync("GCM-99");

        var tab = Tab(state);
        Assert.Contains("Not found or no access: GCM-99", Text(tab.Detail[0]));
        Assert.Single(tab.Rows);

        var back = await PumpAsync(state, new KeyPressed("Esc"));

        Assert.Empty(Tab(back).Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task GivenCachedIssue_WhenGoneReplyReachesTheHandlerWithNoStackedIssue_ThenTheEntryIsDroppedOnlyForNotFoundOrForbidden(
        HttpStatusCode status,
        bool dropped)
    {
        _http.Fixture($"{Site}/GTM-5", "errors/issue-404.json", status);
        await ConnectAsync();
        _cache = new SiteCache(CacheFolder());
        _cache.PutIssue(new CachedIssue(Sample(), DateTimeOffset.UnixEpoch));
        var opened = _reducer.Reduce(ListState("GTM-5"), new KeyPressed("Enter"));
        var effect = opened.Effects.OfType<LoadIssueDetailEffect>().Single();

        // The reducer never sees the failure with a stacked issue to name the key: only the handler can drop it.
        await _handler!.StartAsync(effect, _cache);

        Assert.Equal(dropped, _cache.GetIssue("GTM-5") is null);
    }

    [Fact]
    public async Task GivenLowerCaseKeyTyped_WhenJumping_ThenTheIssueOpensOnTheTabAndIsRemembered()
    {
        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json")
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json")
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json");
        await ConnectAsync();

        var state = await JumpAsync(ListState("A-1"), "gcm-6");

        var tab = Tab(state);
        Assert.Equal("GCM-6", Assert.Single(tab.Detail).Key);
        Assert.Contains("Comments 3", Text(Top(state)));
        Assert.Equal(["GCM-6"], state.Session.RecentKeys);
        Assert.Null(state.Jump);
        Assert.Equal(InputMode.Normal, state.Mode);
    }

    [Fact]
    public async Task GivenAKeyThatDoesNotExist_WhenJumping_ThenTheDetailSaysNotFoundAndNothingIsRemembered()
    {
        _http.Fixture($"{Site}/GCM-99", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/comment", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/remotelink", "errors/issue-404.json", HttpStatusCode.NotFound);
        await ConnectAsync();

        var state = await JumpAsync(ListState("A-1"), "GCM-99");

        Assert.Contains("Not found or no access: GCM-99", Text(Top(state)));
        Assert.Empty(state.Session.RecentKeys);
    }

    [Fact]
    public async Task GivenNoTabOpen_WhenJumping_ThenATabForTheKeyOpensWithTheIssueOnTop()
    {
        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json")
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json")
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json");
        await ConnectAsync();
        var profiles = ProfileCatalog.Empty.With(_profile);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_profile) };

        var state = await JumpAsync(start, "GCM-6");

        var tab = Tab(state);
        Assert.Equal("key = GCM-6", tab.Definition.SourceValue);
        Assert.Equal("GCM-6", Assert.Single(tab.Detail).Key);
    }

    [Fact]
    public async Task GivenRateLimitedJira_WhenOpened_ThenTheRateLimitTextShows()
    {
        _http.Fixture($"{Site}/GCM-6", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests)
            .Fixture($"{Site}/GCM-6/comment", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests)
            .Fixture($"{Site}/GCM-6/remotelink", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests);
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        Assert.Contains("rate limiting", Text(Top(state)));
    }

    [Fact]
    public async Task GivenUnauthorizedJira_WhenOpened_ThenTheTokenHintShows()
    {
        _http.Fixture($"{Site}/GCM-6", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized)
            .Fixture($"{Site}/GCM-6/comment", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized)
            .Fixture($"{Site}/GCM-6/remotelink", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized);
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        Assert.Contains("Check the token", Text(Top(state)));
    }

    [Fact]
    public async Task GivenNoConnection_WhenOpened_ThenTheDetailSaysSoInsteadOfLoadingForever()
    {
        _connections = new SiteConnections(new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));
        _runner = new NetworkRunner(_connections, new QueueCommandSink(_posted), CancellationToken.None);
        _handler = new IssueDetailLoadHandler(_runner);

        var state = await OpenAsync("GCM-6");

        var detail = Top(state);
        Assert.IsType<Failed>(detail.IssueLoad);
        Assert.Contains("Not connected to Jira", Text(detail));
    }

    [Fact]
    public async Task GivenRepliesInFlight_WhenTheTabIsClosed_ThenNothingIsPostedForIt()
    {
        var gate = new TaskCompletionSource();

        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json", gate: gate.Task)
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json", gate: gate.Task)
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json", gate: gate.Task);
        await ConnectAsync();
        var opened = _reducer.Reduce(ListState("GCM-6"), new KeyPressed("Enter"));
        var runs = opened.Effects.Select(effect => _handler!.StartAsync(effect)).ToList();
        var tab = Tab(opened.State);

        _runner!.CancelScope(tab.Id.Value);
        gate.SetResult();
        await Task.WhenAll(runs);

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task GivenStoryWithAParentEpic_WhenPressingOpenParent_ThenTheEpicLoadsOnTheStackAndEscComesBack()
    {
        ServeStory("GCM-10", "gcm10");
        ServeEpic("GCM-8", "gcm8", "hierarchy/search-epic-children-gcm.json");
        await ConnectAsync();
        var story = await OpenAsync("GCM-10");

        var parent = await PumpAsync(await PumpAsync(story, new KeyPressed("Space")), new KeyPressed("p"));

        Assert.Equal(["GCM-10", "GCM-8"], Tab(parent).Detail.Select(detail => detail.Key));
        Assert.Contains("Child issues 2", Text(Top(parent)));

        var back = await PumpAsync(parent, new KeyPressed("Esc"));

        Assert.Equal(["GCM-10"], Tab(back).Detail.Select(detail => detail.Key));
    }

    [Fact]
    public async Task GivenCursorOnASubtaskRow_WhenPressingEnter_ThenItIsRequestedByKeyAndAMissingOneShowsNotFound()
    {
        ServeStory("GCM-10", "gcm10");
        _http.Fixture($"{Site}/GCM-18", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-18/comment", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-18/remotelink", "errors/issue-404.json", HttpStatusCode.NotFound);
        await ConnectAsync();
        var state = await OpenAsync("GCM-10");

        while (Top(state).Document!.Targets[Top(state).Cursor] is not { Kind: DetailTargetKind.Issue, Value: "GCM-18" })
        {
            state = await PumpAsync(state, new KeyPressed("j"));
        }

        var opened = await PumpAsync(state, new KeyPressed("Enter"));

        Assert.Equal(["GCM-10", "GCM-18"], Tab(opened).Detail.Select(detail => detail.Key));
        Assert.Contains("Not found or no access: GCM-18", Text(Top(opened)));
        Assert.Contains(_http.Requests, uri => uri.AbsolutePath == $"{Site}/GCM-18");

        var back = await PumpAsync(opened, new KeyPressed("Esc"));

        Assert.Equal("GCM-10", Top(back).Key);
        Assert.Contains("Sub-tasks 2", Text(Top(back)));
    }

    private void ServeStory(string key, string fixture)
    {
        _http.Fixture($"{Site}/{key}", $"issues/issue-{fixture}.json")
            .Fixture($"{Site}/{key}/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/{key}/remotelink", "links/remotelink-list-gcm6.json");
    }

    private void ServeEpic(string key, string fixture, string childrenFixture)
    {
        _http.Fixture($"{Site}/{key}", $"issues/issue-{fixture}.json")
            .Fixture($"{Site}/{key}/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/{key}/remotelink", "links/remotelink-list-empty.json")
            .Fixture(SearchPath, childrenFixture);
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Tab(state).Detail[^1];
    }

    private static string Text(DetailState detail)
    {
        var side = detail.Document!.Side?.Lines ?? [];

        return string.Join('\n', detail.Document.Lines.Concat(side).Select(line => line.PlainText));
    }

    private AppState ListState(string key)
    {
        var profiles = ProfileCatalog.Empty.With(_profile);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_profile) };

        state = _reducer.Reduce(state, new ViewportChanged(20, 100)).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = Tab(opened.State);
        var row = new IssueSummary(
            "1",
            key,
            "Row summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
        var page = new IssuePage([row], null, null);

        return _reducer.Reduce(
            opened.State,
            new ViewPageLoaded(tab.Id, tab.RequestId, page, DateTimeOffset.UtcNow)).State;
    }

    private async Task<AppState> OpenAsync(string key)
    {
        return await PumpAsync(ListState(key), new KeyPressed("Enter"));
    }

    /// <summary>Opens the jump prompt, types <paramref name="text"/> and presses Enter, then runs the loads.</summary>
    private async Task<AppState> JumpAsync(AppState state, string text)
    {
        var typed = _reducer.Reduce(state, new KeyPressed("Space")).State;

        typed = _reducer.Reduce(typed, new KeyPressed("g")).State;

        foreach (var character in text)
        {
            typed = _reducer.Reduce(typed, new KeyPressed(character.ToString())).State;
        }

        return await PumpAsync(typed, new KeyPressed("Enter"));
    }

    private Task StartEffect(Effect effect)
    {
        return effect is LoadFormMetadataEffect form
            ? new FormMetadataHandler(_runner!).StartAsync(form)
            : _handler!.StartAsync(effect);
    }

    private async Task<AppState> PumpAsync(AppState state, Command command)
    {
        var pending = new Queue<Command>([command]);

        while (pending.Count > 0)
        {
            var result = _reducer.Reduce(state, pending.Dequeue());

            state = result.State;
            _posted.Clear();

            var runs = result.Effects.Select(StartEffect).ToList();

            await Task.WhenAll(runs);

            foreach (var posted in _posted)
            {
                pending.Enqueue(posted);
            }
        }

        return state;
    }

    private async Task ConnectAsync()
    {
        var store = new FakeSecretStore();
        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        _connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(_http, options));

        await _connections.ActivateAsync(_profile, 1, CancellationToken.None);

        _runner = new NetworkRunner(_connections, new QueueCommandSink(_posted), CancellationToken.None);
        _handler = new IssueDetailLoadHandler(_runner);
    }
}
