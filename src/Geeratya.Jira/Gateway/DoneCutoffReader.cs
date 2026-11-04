using System.Collections.Concurrent;
using Geeratya.Jira.Gateway.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Gateway;

/// <summary>
/// Reads the Done cutoff of a board through <see cref="BoardViewClient"/> and keeps the answers for the connection
/// (one reader per transport, shared by every tab): the cloud id and each board's view id for good, the cutoff for
/// <see cref="ValueLifetime"/> (the user may change it in Jira) and a failure for <see cref="FailureLifetime"/> (so a
/// board that cannot be read is not asked again on every refresh). Lookups run one at a time, so tabs loading the
/// same board together cost one chain. A lookup that takes longer than <see cref="LookupTimeout"/> fails with
/// <see cref="JiraErrorKind.Timeout"/>: the answer is optional and must never hold a board load back.
/// </summary>
public sealed class DoneCutoffReader(
    BoardViewClient client,
    TimeProvider? clock = null,
    TimeSpan? lookupTimeout = null) : IDisposable
{
    /// <summary>How long a read cutoff is reused before it is read again.</summary>
    public static readonly TimeSpan ValueLifetime = TimeSpan.FromMinutes(5);

    /// <summary>How long a failure is remembered.</summary>
    public static readonly TimeSpan FailureLifetime = TimeSpan.FromMinutes(1);

    /// <summary>The longest a lookup may take before the caller goes on without it (unless a reader sets its own).</summary>
    public static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(3);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly TimeSpan _timeout = lookupTimeout ?? LookupTimeout;
    private readonly SemaphoreSlim _lookup = new(1, 1);
    private readonly ConcurrentDictionary<long, string> _viewIds = new();
    private readonly ConcurrentDictionary<long, Remembered> _answers = new();
    private string? _cloudId;

    /// <summary>
    /// The cutoff of the Kanban board <paramref name="boardId"/>. Never throws except for the caller's own
    /// cancellation; every other problem is a failed result.
    /// </summary>
    public async Task<JiraResult<BoardViewCutoff>> ReadAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (Fresh(boardId) is { } known)
        {
            return known;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            await _lookup.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TimedOut();
        }

        try
        {
            // Another load may have finished the same chain while this one waited.
            if (Fresh(boardId) is { } meanwhile)
            {
                return meanwhile;
            }

            return await ReadChainAsync(boardId, priority, timeout.Token, cancellationToken);
        }
        finally
        {
            _lookup.Release();
        }
    }

    public void Dispose()
    {
        _lookup.Dispose();
    }

    private async Task<JiraResult<BoardViewCutoff>> ReadChainAsync(
        long boardId,
        JiraPriority priority,
        CancellationToken timeoutToken,
        CancellationToken cancellationToken)
    {
        JiraResult<BoardViewCutoff> result;

        try
        {
            result = await ChainAsync(boardId, priority, timeoutToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = TimedOut();
        }

        var lifetime = result.IsSuccess ? ValueLifetime : FailureLifetime;
        _answers[boardId] = new Remembered(result, _clock.GetUtcNow() + lifetime);

        return result;
    }

    private async Task<JiraResult<BoardViewCutoff>> ChainAsync(
        long boardId,
        JiraPriority priority,
        CancellationToken cancellationToken)
    {
        if (!_viewIds.TryGetValue(boardId, out var viewId))
        {
            var found = await FindViewIdAsync(boardId, priority, cancellationToken);

            if (!found.IsSuccess)
            {
                return JiraResults.Failure<BoardViewCutoff>(found.Error);
            }

            viewId = found.Value;
            _viewIds[boardId] = viewId;
        }

        return await client.GetCutoffAsync(viewId, cancellationToken, priority);
    }

    private async Task<JiraResult<string>> FindViewIdAsync(
        long boardId,
        JiraPriority priority,
        CancellationToken cancellationToken)
    {
        if (_cloudId is null)
        {
            var cloud = await client.GetCloudIdAsync(cancellationToken, priority);

            if (!cloud.IsSuccess)
            {
                return cloud;
            }

            _cloudId = cloud.Value;
        }

        return await client.GetViewIdAsync(_cloudId, boardId, cancellationToken, priority);
    }

    private JiraResult<BoardViewCutoff>? Fresh(long boardId)
    {
        if (_answers.TryGetValue(boardId, out var remembered) && _clock.GetUtcNow() < remembered.Expires)
        {
            return remembered.Result;
        }

        return null;
    }

    private static JiraResult<BoardViewCutoff> TimedOut()
    {
        var error = new JiraError(JiraErrorKind.Timeout, null, "The done cutoff lookup took too long.");

        return JiraResults.Failure<BoardViewCutoff>(error);
    }

    private sealed record Remembered(JiraResult<BoardViewCutoff> Result, DateTimeOffset Expires);
}
