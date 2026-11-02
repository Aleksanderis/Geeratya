using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Tui;

/// <summary>
/// Turns a <see cref="LoadSourcesEffect"/> into the boards and saved filters the source picker offers. The work runs
/// on the <see cref="NetworkRunner"/> in the user lane under its own scope (it belongs to no tab), so a profile
/// change cancels it and a reply from an older site is dropped before it is posted.
/// </summary>
public sealed class SourceLoadHandler
{
    /// <summary>The runner scope of source loads; tab ids are positive, so it never collides with one.</summary>
    public const long Scope = -1;

    private const int MaxBoardPages = 20;

    private const string NoConnectionMessage =
        "Not connected to Jira: check the profile and its API token (:profile), then reopen the picker.";

    private readonly NetworkRunner _runner;

    public SourceLoadHandler(NetworkRunner runner)
    {
        _runner = runner;
    }

    /// <summary>Starts the requested loads and returns the task that ends when both are posted (or dropped).</summary>
    public Task StartAsync(LoadSourcesEffect effect)
    {
        var tasks = new List<Task>();

        if (effect.Boards)
        {
            tasks.Add(_runner.Start(Scope, BoardsAsync, UnavailableBoards));
        }

        if (effect.Filters)
        {
            tasks.Add(_runner.Start(Scope, FiltersAsync, UnavailableFilters));
        }

        return Task.WhenAll(tasks);
    }

    private static Command UnavailableBoards()
    {
        return Failure(SourceListKind.Boards, new JiraError(JiraErrorKind.Unexpected, null, NoConnectionMessage));
    }

    private static Command UnavailableFilters()
    {
        return Failure(SourceListKind.Filters, new JiraError(JiraErrorKind.Unexpected, null, NoConnectionMessage));
    }

    private static SourceListFailed Failure(SourceListKind kind, JiraError error)
    {
        return new SourceListFailed(kind, error);
    }

    private static async Task<Command?> BoardsAsync(JiraServices services, CancellationToken token)
    {
        try
        {
            var boards = new List<BoardSummary>();
            var startAt = 0;
            var query = new BoardListQuery();

            for (var page = 0; page < MaxBoardPages; page++)
            {
                var result = await services.Boards.ListBoardsAsync(query, startAt, token);

                if (!result.IsSuccess)
                {
                    return Failure(SourceListKind.Boards, result.Error);
                }

                boards.AddRange(result.Value.Items);

                if (result.Value.NextStartAt is not { } next)
                {
                    break;
                }

                startAt = next;
            }

            return new BoardsLoaded(boards);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unexpected(SourceListKind.Boards, exception);
        }
    }

    private static async Task<Command?> FiltersAsync(JiraServices services, CancellationToken token)
    {
        try
        {
            var favourites = await services.Filters.GetFavouritesAsync(token);

            if (!favourites.IsSuccess)
            {
                return Failure(SourceListKind.Filters, favourites.Error);
            }

            var own = await services.Filters.GetMyAsync(token);

            if (!own.IsSuccess)
            {
                return Failure(SourceListKind.Filters, own.Error);
            }

            return new FiltersLoaded(SourceChoices.MergeFilters(favourites.Value, own.Value));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unexpected(SourceListKind.Filters, exception);
        }
    }

    private static SourceListFailed Unexpected(SourceListKind kind, Exception exception)
    {
        var message = $"Loading failed unexpectedly ({exception.GetType().Name}).";

        return Failure(kind, new JiraError(JiraErrorKind.Unexpected, null, message));
    }
}
