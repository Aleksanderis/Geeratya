using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Geeratya.Core.Views;

namespace Geeratya.Core.Cache;

/// <summary>
/// The cache of one profile: the first page of every opened view (most recent 32 in memory) and the detail of
/// every opened issue (most recent 200 in memory), both on disk below the cache folder. Host-owned and not
/// part of the app state (<c>docs/internal/architecture.md</c>, "Cache and refresh").
/// Reads and writes are meant for background threads; <see cref="Put"/> only records the page and schedules the
/// write, which runs debounced on a timer thread. A disposed cache ignores everything and never touches the disk
/// again, so a pending write cannot recreate a folder that was deleted. A cache is never a reason to fail a read:
/// a file that cannot be read, parsed or mapped is a miss, and a file of another schema is deleted.
/// </summary>
public sealed partial class SiteCache : IDisposable
{
    /// <summary>The most views kept in memory; the least recently used one leaves first.</summary>
    public const int MaxViews = 32;

    /// <summary>The most issue details kept in memory; the least recently used one leaves first.</summary>
    public const int MaxIssues = 200;

    /// <summary>The default size of a profile's cache folder.</summary>
    public const long DefaultMaxDiskBytes = 20L * 1024 * 1024;

    private const string ViewFilePrefix = "view-";
    private const string IssueFilePrefix = "issue-";
    private const string FileExtension = ".json";
    private const string TemporaryExtension = ".tmp";
    private const int NameHashLength = 32;

    // An ADF description nests up to 64 levels, two JSON levels each; the default depth limit (64) is too low.
    private const int IssueJsonMaxDepth = 512;

    private static readonly CacheJsonContext _issueContext = new(new JsonSerializerOptions
    {
        MaxDepth = IssueJsonMaxDepth,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });

    private static readonly TimeSpan _defaultDebounce = TimeSpan.FromSeconds(2);

    private readonly string _folder;
    private readonly IFilePermissions _permissions;
    private readonly TimeSpan _debounce;
    private readonly long _maxDiskBytes;
    private readonly object _memoryLock = new();
    private readonly object _diskLock = new();
    private readonly Dictionary<CacheKey, MemoryEntry<CachedView>> _views = [];
    private readonly Dictionary<CacheKey, CachedView> _pending = [];
    private readonly Dictionary<string, MemoryEntry<CachedIssue>> _issues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CachedIssue> _pendingIssues = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _removedIssues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _timer;
    private long _stamp;
    private bool _disposed;
    private bool _viewsRemoved;
    private int _viewsRemovedEpoch;
    private bool _failureReported;
    private bool _readProblemReported;

    public SiteCache(
        string folder,
        IFilePermissions? permissions = null,
        TimeSpan? debounce = null,
        long maxDiskBytes = DefaultMaxDiskBytes)
    {
        _folder = folder;
        _permissions = permissions ?? PlainFilePermissions.Instance;
        _debounce = debounce ?? _defaultDebounce;
        _maxDiskBytes = maxDiskBytes;
        _timer = new Timer(FlushOnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Raised (on a background thread) with a one-line notice when a write failed; once per cache.</summary>
    public event Action<string>? WriteFailed;

    /// <summary>
    /// Raised (on a background thread) with a one-line notice when a cache file could not be read (unreadable or
    /// corrupt; a corrupt one is deleted); once per cache. Found while a view or issue is read, so no extra disk scan.
    /// Raised while the disk lock is held: a subscriber must not call back into the cache.
    /// </summary>
    public event Action<string>? ReadProblem;

    /// <summary>The file name of a view: a hash of the key, so no JQL or site data appears in it.</summary>
    public static string ViewFileName(CacheKey key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{key.Source}|{key.SourceValue}"));

        return $"{ViewFilePrefix}{Convert.ToHexStringLower(bytes)[..NameHashLength]}{FileExtension}";
    }

    /// <summary>The file name of an issue detail: a hash of the upper-cased issue key.</summary>
    public static string IssueFileName(string issueKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(issueKey.ToUpperInvariant()));

        return $"{IssueFilePrefix}{Convert.ToHexStringLower(bytes)[..NameHashLength]}{FileExtension}";
    }

    /// <summary>The cached first page of a view: memory first, then disk. Null on a miss or after disposal.</summary>
    public CachedView? Get(CacheKey key)
    {
        lock (_memoryLock)
        {
            if (_disposed)
            {
                return null;
            }

            if (_views.TryGetValue(key, out var entry))
            {
                entry.Stamp = ++_stamp;

                return entry.Value;
            }

            if (_pending.TryGetValue(key, out var pending))
            {
                return pending;
            }

            if (_viewsRemoved)
            {
                // Removed, the files are deleted by the next flush: not a hit.
                return null;
            }
        }

        var read = ReadFromDisk(key);

        if (read is not null)
        {
            Remember(key, read);
        }

        return read;
    }

    /// <summary>Remembers the first page of a view (at most one page of rows) and schedules the disk write.</summary>
    public void Put(CacheKey key, CachedView view)
    {
        var bounded = view.Rows.Count > ViewPaging.PageSize
            ? view with { Rows = view.Rows.Take(ViewPaging.PageSize).ToArray() }
            : view;

        lock (_memoryLock)
        {
            if (_disposed)
            {
                return;
            }

            RememberLocked(key, bounded);
            _pending[key] = bounded;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The cached detail of an issue: memory first, then disk. Null on a miss or after disposal.</summary>
    public CachedIssue? GetIssue(string issueKey)
    {
        lock (_memoryLock)
        {
            if (_disposed)
            {
                return null;
            }

            if (_issues.TryGetValue(issueKey, out var entry))
            {
                entry.Stamp = ++_stamp;

                return entry.Value;
            }

            if (_pendingIssues.TryGetValue(issueKey, out var pending))
            {
                return pending;
            }

            if (_removedIssues.Contains(issueKey))
            {
                // Removed, the file is deleted by the next flush: not a hit.
                return null;
            }
        }

        var read = ReadIssueFromDisk(issueKey);

        return read is not null && RememberIssueIfAbsent(read) ? read : null;
    }

    /// <summary>Remembers a successfully loaded issue detail and schedules the disk write.</summary>
    public void PutIssue(CachedIssue issue)
    {
        lock (_memoryLock)
        {
            if (_disposed)
            {
                return;
            }

            RememberIssueLocked(issue);
            _pendingIssues[issue.Detail.Key] = issue;
            _removedIssues.Remove(issue.Detail.Key);
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Forgets the detail of an issue that is gone or forbidden (404/403): at once in memory, and the file is deleted
    /// with the next flush (so the UI thread never touches the disk). A later <see cref="PutIssue"/> of the same key
    /// cancels the deletion.
    /// </summary>
    public void RemoveIssue(string issueKey)
    {
        lock (_memoryLock)
        {
            if (_disposed)
            {
                return;
            }

            _issues.Remove(issueKey);
            _pendingIssues.Remove(issueKey);
            _removedIssues.Add(issueKey);
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Forgets every cached first page of a view: at once in memory, and the files are deleted with the next flush.
    /// A write may have changed any issue, and the app cannot tell which cached pages hold it
    /// (<c>docs/internal/jira-layer.md</c>, "Cache keys"). A later <see cref="Put"/> caches again.
    /// </summary>
    public void RemoveViews()
    {
        lock (_memoryLock)
        {
            if (_disposed)
            {
                return;
            }

            _views.Clear();
            _pending.Clear();
            _viewsRemoved = true;
            _viewsRemovedEpoch++;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Writes pending views and issues now (the debounce timer, and the exit of the application, call it).
    /// </summary>
    public void Flush()
    {
        lock (_diskLock)
        {
            Dictionary<CacheKey, CachedView> batch;
            List<CachedIssue> issues;
            List<string> removed;
            bool removeViews;
            int epoch;

            lock (_memoryLock)
            {
                if (_disposed || (!_viewsRemoved && _pending.Count + _pendingIssues.Count + _removedIssues.Count == 0))
                {
                    return;
                }

                // The flag stays set until the files are gone, so a read in between is still a miss.
                removeViews = _viewsRemoved;
                epoch = _viewsRemovedEpoch;

                batch = new Dictionary<CacheKey, CachedView>(_pending);
                issues = [.. _pendingIssues.Values];
                removed = [.. _removedIssues];
                _pending.Clear();
                _pendingIssues.Clear();
                _removedIssues.Clear();
            }

            if (removeViews)
            {
                DeleteViewFiles();
                ClearViewsRemoved(epoch);
            }

            foreach (var key in removed)
            {
                DeleteQuietly(Path.Combine(_folder, IssueFileName(key)));
            }

            WriteAll(batch, issues);
        }
    }

    /// <summary>
    /// Drops the memory part and any pending write at once, without waiting for file work in progress (the UI
    /// thread calls it on a profile switch). A batch already being written may still finish; the disk stays.
    /// Pending removals (an issue that is gone, removed views) are not lost: they are deleted on a pool thread,
    /// the returned task completes when that is done. A cache opened later for the same folder has its own disk
    /// lock, so the two can overlap briefly; at worst that costs one stale hit or one lost page. Not awaited at
    /// exit: a removal not yet done then leaves a file that is only a stale cache entry.
    /// </summary>
    public Task Discard()
    {
        var (removed, removeViews) = DiscardMemory();

        if (removed.Count == 0 && !removeViews)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            lock (_diskLock)
            {
                DeletePending(removed, removeViews);
            }
        });
    }

    /// <summary>
    /// Like <see cref="Discard"/>, but waits for a write in progress, so nothing can touch the folder afterwards
    /// (a folder that is about to be deleted, so pending removals are not carried out).
    /// </summary>
    public void Dispose()
    {
        lock (_diskLock)
        {
            DiscardMemory();
        }
    }

    private void FlushOnTimer(object? state)
    {
        Flush();
    }

    private bool IsDisposed()
    {
        lock (_memoryLock)
        {
            return _disposed;
        }
    }

    private sealed class MemoryEntry<TValue>(TValue value)
    {
        public TValue Value { get; } = value;

        public long Stamp { get; set; }
    }
}
