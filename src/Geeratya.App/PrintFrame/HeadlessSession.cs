using System.Collections.Concurrent;
using Geeratya.Core.Detail;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.App.PrintFrame;

/// <summary>
/// Runs the reducer and the Jira load handlers without a terminal: a command goes in, its read effects run against
/// the connection and their replies are fed back until nothing is left to load. Only reads run (no cache, no writes,
/// no files), so it can draw a real screen for <c>--print-frame</c>.
/// </summary>
public sealed class HeadlessSession : ICommandSink, IDisposable
{
    private const int MaximumSteps = 500;

    private readonly AppReducer _reducer;
    private readonly NetworkRunner _network;
    private readonly ViewLoadHandler _views;
    private readonly IssueDetailLoadHandler _details;
    private readonly SourceLoadHandler _sources;
    private readonly ConcurrentQueue<Command> _posted = new();

    public HeadlessSession(AppReducer reducer, SiteConnections connections, CancellationToken cancellationToken)
    {
        _reducer = reducer;
        _network = new NetworkRunner(connections, this, cancellationToken);
        _views = new ViewLoadHandler(_network);
        _details = new IssueDetailLoadHandler(_network);
        _sources = new SourceLoadHandler(_network);
    }

    public void Post(Command command)
    {
        _posted.Enqueue(command);
    }

    /// <summary>Applies the command, then every reply it causes, and returns the settled state.</summary>
    public async Task<AppState> RunAsync(AppState state, Command command)
    {
        var pending = new Queue<Command>([command]);
        var steps = 0;

        while (pending.Count > 0 && steps++ < MaximumSteps)
        {
            var result = _reducer.Reduce(state, pending.Dequeue());

            state = result.State;

            var started = result.Effects.Select(Start).OfType<Task>().ToList();

            await Task.WhenAll(started);

            while (_posted.TryDequeue(out var reply))
            {
                pending.Enqueue(reply);
            }
        }

        return state;
    }

    public void Dispose()
    {
        _network.Dispose();
    }

    private Task? Start(Effect effect)
    {
        return effect switch
        {
            LoadViewPageEffect load => _views.StartAsync(load),
            LoadQuickFiltersEffect quickFilters => _views.StartQuickFiltersAsync(quickFilters),
            LoadIssueDetailEffect or LoadIssueCommentsEffect or LoadRemoteLinksEffect or LoadIssueChildrenEffect
                => _details.StartAsync(effect),
            LoadSourcesEffect sources => _sources.StartAsync(sources),
            LoadSprintsEffect sprints => _sources.StartSprintsAsync(sprints),
            _ => null,
        };
    }
}
