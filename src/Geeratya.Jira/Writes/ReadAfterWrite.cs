using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Writes;

/// <summary>
/// Settles an unknown write outcome: when the write may have been applied, reloads the entity with the caller's
/// read so the caller can compare it with the intended change instead of blindly repeating the write.
/// </summary>
public static class ReadAfterWrite
{
    /// <summary>
    /// Runs <paramref name="reload"/> only when <paramref name="write"/> failed with an unknown outcome; returns
    /// null for a success or a failure that is certain (nothing was applied), so no extra request is made.
    /// </summary>
    public static async Task<JiraResult<TRead>?> ReloadIfUnknownAsync<TWrite, TRead>(
        WriteResult<TWrite> write,
        Func<CancellationToken, Task<JiraResult<TRead>>> reload,
        CancellationToken cancellationToken)
    {
        if (write.IsSuccess || !write.Error.IsOutcomeUnknown)
        {
            return null;
        }

        return await reload(cancellationToken);
    }
}
