
namespace Geeratya.Core.Cache;

public sealed partial class SiteCache
{
    private (List<string> Removed, bool RemoveViews) DiscardMemory()
    {
        List<string> removed;
        bool removeViews;

        lock (_memoryLock)
        {
            _disposed = true;
            removed = [.. _removedIssues];
            removeViews = _viewsRemoved;
            _views.Clear();
            _pending.Clear();
            _issues.Clear();
            _pendingIssues.Clear();
            _removedIssues.Clear();
            _viewsRemoved = false;
        }

        _timer.Dispose();

        return (removed, removeViews);
    }

    private void DeletePending(List<string> removed, bool removeViews)
    {
        if (removeViews)
        {
            DeleteViewFiles();
        }

        foreach (var key in removed)
        {
            DeleteQuietly(Path.Combine(_folder, IssueFileName(key)));
        }
    }

    /// <summary>Ends the "views removed" state, unless another removal came in while the files were deleted.</summary>
    private void ClearViewsRemoved(int epoch)
    {
        lock (_memoryLock)
        {
            if (_viewsRemovedEpoch == epoch)
            {
                _viewsRemoved = false;
            }
        }
    }

    private void Remember(CacheKey key, CachedView view)
    {
        lock (_memoryLock)
        {
            if (!_disposed)
            {
                RememberLocked(key, view);
            }
        }
    }

    private void RememberLocked(CacheKey key, CachedView view)
    {
        _views[key] = new MemoryEntry<CachedView>(view) { Stamp = ++_stamp };

        if (_views.Count > MaxViews)
        {
            EvictLeastRecent(_views);
        }
    }

    /// <summary>Remembers a detail read from disk unless a newer one was stored while the file was read.</summary>
    private bool RememberIssueIfAbsent(CachedIssue issue)
    {
        lock (_memoryLock)
        {
            if (_removedIssues.Contains(issue.Detail.Key))
            {
                return false;
            }

            if (!_disposed && !_issues.ContainsKey(issue.Detail.Key) && !_pendingIssues.ContainsKey(issue.Detail.Key))
            {
                RememberIssueLocked(issue);
            }

            return true;
        }
    }

    private void RememberIssueLocked(CachedIssue issue)
    {
        _issues[issue.Detail.Key] = new MemoryEntry<CachedIssue>(issue) { Stamp = ++_stamp };

        if (_issues.Count > MaxIssues)
        {
            EvictLeastRecent(_issues);
        }
    }

    private static void EvictLeastRecent<TKey, TValue>(Dictionary<TKey, MemoryEntry<TValue>> entries)
        where TKey : notnull
    {
        var oldest = default(TKey);
        var oldestStamp = long.MaxValue;
        var found = false;

        foreach (var (key, entry) in entries)
        {
            if (entry.Stamp < oldestStamp)
            {
                oldest = key;
                oldestStamp = entry.Stamp;
                found = true;
            }
        }

        if (found)
        {
            entries.Remove(oldest!);
        }
    }
}
