using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Geeratya.Core.Views;

namespace Geeratya.Core.Cache;

/// <summary>
/// The cache of one profile: the first page of every opened view (most recent 32 in memory) and the detail of
/// every opened issue (most recent 200 in memory), both on disk below the cache folder. Host-owned and not part of the app state (<c>docs/architecture.md</c>, "Cache and refresh").
/// Reads and writes are meant for background threads; <see cref="Put"/> only records the page and schedules the
/// write, which runs debounced on a timer thread. A disposed cache ignores everything and never touches the disk
/// again, so a pending write cannot recreate a folder that was deleted. A cache is never a reason to fail a read:
/// a file that cannot be read, parsed or mapped is a miss, and a file of another schema is deleted.
/// </summary>
public sealed class SiteCache : IDisposable
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
    private readonly FilePermissions _permissions;
    private readonly TimeSpan _debounce;
    private readonly long _maxDiskBytes;
    private readonly object _memoryLock = new();
    private readonly object _diskLock = new();
    private readonly Dictionary<CacheKey, MemoryEntry<CachedView>> _views = [];
    private readonly Dictionary<CacheKey, CachedView> _pending = [];
    private readonly Dictionary<string, MemoryEntry<CachedIssue>> _issues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CachedIssue> _pendingIssues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _timer;
    private long _stamp;
    private bool _disposed;
    private bool _failureReported;

    public SiteCache(
        string folder,
        FilePermissions? permissions = null,
        TimeSpan? debounce = null,
        long maxDiskBytes = DefaultMaxDiskBytes)
    {
        _folder = folder;
        _permissions = permissions ?? FilePermissions.Plain;
        _debounce = debounce ?? _defaultDebounce;
        _maxDiskBytes = maxDiskBytes;
        _timer = new Timer(FlushOnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Raised (on a background thread) with a one-line notice when a write failed; once per cache.</summary>
    public event Action<string>? WriteFailed;

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
        }

        var read = ReadIssueFromDisk(issueKey);

        if (read is not null)
        {
            RememberIssue(read);
        }

        return read;
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
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Writes pending views and issues now (the debounce timer, and the exit of the application, call it).</summary>
    public void Flush()
    {
        lock (_diskLock)
        {
            Dictionary<CacheKey, CachedView> batch;
            List<CachedIssue> issues;

            lock (_memoryLock)
            {
                if (_disposed || _pending.Count + _pendingIssues.Count == 0)
                {
                    return;
                }

                batch = new Dictionary<CacheKey, CachedView>(_pending);
                issues = [.. _pendingIssues.Values];
                _pending.Clear();
                _pendingIssues.Clear();
            }

            WriteAll(batch, issues);
        }
    }

    /// <summary>
    /// Drops the memory part and any pending write at once, without waiting for file work in progress (the UI
    /// thread calls it on a profile switch). A batch already being written may still finish; the disk stays.
    /// </summary>
    public void Discard()
    {
        lock (_memoryLock)
        {
            _disposed = true;
            _views.Clear();
            _pending.Clear();
            _issues.Clear();
            _pendingIssues.Clear();
        }

        _timer.Dispose();
    }

    /// <summary>
    /// Like <see cref="Discard"/>, but waits for a write in progress, so nothing can touch the folder afterwards
    /// (a folder that is about to be deleted).
    /// </summary>
    public void Dispose()
    {
        lock (_diskLock)
        {
            Discard();
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A cache file that cannot be deleted is left for the next eviction.
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

    private void RememberIssue(CachedIssue issue)
    {
        lock (_memoryLock)
        {
            if (!_disposed)
            {
                RememberIssueLocked(issue);
            }
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

    private CachedView? ReadFromDisk(CacheKey key)
    {
        var path = Path.Combine(_folder, ViewFileName(key));

        lock (_diskLock)
        {
            if (IsDisposed() || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var file = JsonSerializer.Deserialize(stream, CacheJsonContext.Default.CachedViewFile);
                var keyed = file is null ? null : CacheMapper.FromFile(file);

                if (keyed is not null && keyed.Key == key)
                {
                    return keyed.View;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
            {
                // Corrupt or foreign content: fall through to the delete below.
            }

            DeleteQuietly(path);

            return null;
        }
    }

    private CachedIssue? ReadIssueFromDisk(string issueKey)
    {
        var path = Path.Combine(_folder, IssueFileName(issueKey));

        lock (_diskLock)
        {
            if (IsDisposed() || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var file = JsonSerializer.Deserialize(stream, _issueContext.CachedIssueFile);
                var issue = file is null ? null : IssueCacheMapper.FromFile(file);

                if (issue is not null && string.Equals(issue.Detail.Key, issueKey, StringComparison.OrdinalIgnoreCase))
                {
                    return issue;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
            {
                // Corrupt or foreign content: fall through to the delete below.
            }

            DeleteQuietly(path);

            return null;
        }
    }

    private void WriteAll(Dictionary<CacheKey, CachedView> batch, List<CachedIssue> issues)
    {
        try
        {
            _permissions.CreateDirectory(_folder);

            foreach (var (key, view) in batch)
            {
                WriteFile(
                    Path.Combine(_folder, ViewFileName(key)),
                    CacheMapper.ToFile(key, view),
                    CacheJsonContext.Default.CachedViewFile);
            }

            foreach (var issue in issues)
            {
                WriteIssue(issue);
            }

            EvictOldest();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(exception);
        }
    }

    private void WriteIssue(CachedIssue issue)
    {
        var path = Path.Combine(_folder, IssueFileName(issue.Detail.Key));

        try
        {
            WriteFile(path, IssueCacheMapper.ToFile(issue), _issueContext.CachedIssueFile);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // A detail that cannot be serialized is simply not cached; the temporary file may be left behind.
            DeleteQuietly($"{path}{TemporaryExtension}");
        }
    }

    private void WriteFile<TFile>(string path, TFile file, JsonTypeInfo<TFile> typeInfo)
    {
        var temporary = $"{path}{TemporaryExtension}";

        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _permissions.RestrictFile(temporary);
            JsonSerializer.Serialize(stream, file, typeInfo);
        }

        File.Move(temporary, path, true);
    }

    /// <summary>Deletes the oldest files until the folder fits the size cap.</summary>
    private void EvictOldest()
    {
        var files = new DirectoryInfo(_folder).GetFiles().OrderBy(file => file.LastWriteTimeUtc).ToList();
        var total = files.Sum(file => file.Length);

        foreach (var file in files)
        {
            if (total <= _maxDiskBytes)
            {
                break;
            }

            total -= file.Length;
            DeleteQuietly(file.FullName);
        }
    }

    private void ReportFailure(Exception exception)
    {
        lock (_memoryLock)
        {
            if (_failureReported)
            {
                return;
            }

            _failureReported = true;
        }

        WriteFailed?.Invoke(
            $"The local cache could not be written ({exception.GetType().Name}); views still load from Jira.");
    }

    private sealed class MemoryEntry<TValue>(TValue value)
    {
        public TValue Value { get; } = value;

        public long Stamp { get; set; }
    }
}
