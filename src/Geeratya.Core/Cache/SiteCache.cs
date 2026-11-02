using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Geeratya.Core.Views;

namespace Geeratya.Core.Cache;

/// <summary>
/// The cache of one profile: the first page of every opened view, in memory (most recent 32) and on disk below
/// the cache folder. Host-owned and not part of the app state (<c>docs/architecture.md</c>, "Cache and refresh").
/// Reads and writes are meant for background threads; <see cref="Put"/> only records the page and schedules the
/// write, which runs debounced on a timer thread. A disposed cache ignores everything and never touches the disk
/// again, so a pending write cannot recreate a folder that was deleted. A cache is never a reason to fail a read:
/// a file that cannot be read, parsed or mapped is a miss, and a file of another schema is deleted.
/// </summary>
public sealed class SiteCache : IDisposable
{
    /// <summary>The most views kept in memory; the least recently used one leaves first.</summary>
    public const int MaxViews = 32;

    /// <summary>The default size of a profile's cache folder.</summary>
    public const long DefaultMaxDiskBytes = 20L * 1024 * 1024;

    private const string ViewFilePrefix = "view-";
    private const string FileExtension = ".json";
    private const string TemporaryExtension = ".tmp";
    private const int NameHashLength = 32;

    private static readonly TimeSpan _defaultDebounce = TimeSpan.FromSeconds(2);

    private readonly string _folder;
    private readonly FilePermissions _permissions;
    private readonly TimeSpan _debounce;
    private readonly long _maxDiskBytes;
    private readonly object _memoryLock = new();
    private readonly object _diskLock = new();
    private readonly Dictionary<CacheKey, MemoryEntry> _views = [];
    private readonly Dictionary<CacheKey, CachedView> _pending = [];
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

                return entry.View;
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

    /// <summary>Writes pending views now (the debounce timer, and the exit of the application, call it).</summary>
    public void Flush()
    {
        lock (_diskLock)
        {
            Dictionary<CacheKey, CachedView> batch;

            lock (_memoryLock)
            {
                if (_disposed || _pending.Count == 0)
                {
                    return;
                }

                batch = new Dictionary<CacheKey, CachedView>(_pending);
                _pending.Clear();
            }

            WriteAll(batch);
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
        _views[key] = new MemoryEntry(view) { Stamp = ++_stamp };

        if (_views.Count > MaxViews)
        {
            EvictLeastRecent();
        }
    }

    private void EvictLeastRecent()
    {
        var oldest = default(CacheKey);
        var oldestStamp = long.MaxValue;

        foreach (var (key, entry) in _views)
        {
            if (entry.Stamp < oldestStamp)
            {
                oldest = key;
                oldestStamp = entry.Stamp;
            }
        }

        if (oldest is not null)
        {
            _views.Remove(oldest);
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

    private void WriteAll(Dictionary<CacheKey, CachedView> batch)
    {
        try
        {
            _permissions.CreateDirectory(_folder);

            foreach (var (key, view) in batch)
            {
                WriteFile(Path.Combine(_folder, ViewFileName(key)), CacheMapper.ToFile(key, view));
            }

            EvictOldest();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(exception);
        }
    }

    private void WriteFile(string path, CachedViewFile file)
    {
        var temporary = $"{path}{TemporaryExtension}";

        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _permissions.RestrictFile(temporary);
            JsonSerializer.Serialize(stream, file, CacheJsonContext.Default.CachedViewFile);
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

    private sealed class MemoryEntry(CachedView view)
    {
        public CachedView View { get; } = view;

        public long Stamp { get; set; }
    }
}
