using System.Net;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests.Fakes;

/// <summary>
/// Opens a board the way the app does (reducer, effect handler, transport, recorded replies) and returns the loaded
/// state, so renderer tests run on the real shape of both project kinds.
/// </summary>
public static class BoardStateFixtures
{
    // A team-managed board says "simple", so opening it probes the sprint list once; Kanban answers 400.
    private const string KanbanProbeReply = """{"errorMessages":["The board does not support sprints"],"errors":{}}""";

    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");

    /// <summary>The company-managed Kanban board (4 columns, Backlog first, 24 issues).</summary>
    public static Task<AppState> CompanyManagedAsync(int viewportRows)
    {
        return OpenAsync("board-gcm-config", ["rows-gcm-issues"], "4", "GCM board", viewportRows, 0);
    }

    /// <summary>The team-managed board (4 columns, Backlog third, 16 issues).</summary>
    public static Task<AppState> TeamManagedAsync(int viewportRows)
    {
        return OpenAsync("board-gtm-config", ["rows-gtm-issues"], "3", "GTM board", viewportRows, 0, KanbanProbeReply);
    }

    /// <summary>
    /// A board whose issues carry their <c>subtasks</c> stubs: the sub-tasks are cards too, or, with
    /// <paramref name="filtered"/>, the board filter leaves them out and only the parents list them.
    /// </summary>
    public static Task<AppState> WithSubtasksAsync(string kind, bool filtered, int viewportRows, int viewportColumns = 0)
    {
        var page = $"rows-{kind}-issues-subtasks{(filtered ? "-filtered" : string.Empty)}";

        return kind == "gcm"
            ? OpenAsync("board-gcm-config", [page], "4", "GCM board", viewportRows, viewportColumns)
            : OpenAsync("board-gtm-config", [page], "3", "GTM board", viewportRows, viewportColumns, KanbanProbeReply);
    }

    private static async Task<AppState> OpenAsync(
        string configuration,
        string[] pages,
        string boardId,
        string title,
        int viewportRows,
        int viewportColumns,
        string? probeReply = null)
    {
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, BoardFixtureFiles.Read(configuration));

        if (probeReply is not null)
        {
            http.Reply(HttpStatusCode.BadRequest, probeReply);
        }

        foreach (var page in pages)
        {
            http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read(page));
        }

        var store = new FakeSecretStore();

        await store.SetAsync(_profile.CredentialKey, "token", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        using var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_profile, 2, CancellationToken.None);

        var posted = new List<Command>();

        using var runner = new NetworkRunner(connections, new ListCommandSink(posted), CancellationToken.None);

        var handler = new ViewLoadHandler(runner);
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };
        var definition = new ViewDefinition(ViewSourceKind.Board, boardId, ViewLayout.Columns, title);
        var pending = new Queue<Command>([new ViewOpenRequested(definition), new ViewportChanged(viewportRows, viewportColumns)]);

        while (pending.Count > 0)
        {
            var result = reducer.Reduce(state, pending.Dequeue());

            state = result.State;
            posted.Clear();

            foreach (var effect in result.Effects.OfType<LoadViewPageEffect>())
            {
                await handler.StartAsync(effect);
            }

            foreach (var command in posted)
            {
                pending.Enqueue(command);
            }
        }

        return state;
    }
}
