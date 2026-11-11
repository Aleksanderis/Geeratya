using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Geeratya.Core.Storage;

namespace Geeratya.Core.Cache;

public sealed partial class SiteCache
{
    private const int MaxYields = 50;

    // `locked`: the caller already holds the disk lock (a discard after disposal), so the files are deleted directly.
    private void DeleteViewFiles(bool locked = false)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(_folder, $"{ViewFilePrefix}*{FileExtension}"))
            {
                if (locked)
                {
                    DeleteQuietly(path);
                }
                else
                {
                    WithDisk(() => DeleteQuietly(path));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be listed (or does not exist yet) has nothing to delete.
        }
    }

    /// <summary>Runs one file operation under the disk lock, unless the cache was disposed meanwhile.</summary>
    private void WithDisk(Action operation)
    {
        YieldToReads();

        lock (_diskLock)
        {
            if (!IsDisposed())
            {
                operation();
            }
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

    private CachedView? ReadFromDisk(CacheKey key)
    {
        var path = Path.Combine(_folder, ViewFileName(key));

        Interlocked.Increment(ref _readsWaiting);

        lock (_diskLock)
        {
            Interlocked.Decrement(ref _readsWaiting);

            if (IsDisposed() || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = AtomicFile.OpenRead(path);
                var file = JsonSerializer.Deserialize(stream, CacheJsonContext.Default.CachedViewFile);
                var keyed = file is null ? null : CacheMapper.FromFile(file);

                if (keyed is not null && keyed.Key == key)
                {
                    Touch(path);

                    return keyed.View;
                }
            }
            catch (FileNotFoundException)
            {
                // Another instance replaced or removed the file after the existence check: a miss, not a problem.
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ReportReadProblem(exception);

                return null;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
            {
                // Corrupt or foreign content: report it, then fall through to the delete below.
                ReportReadProblem(exception);
            }

            DeleteQuietly(path);

            return null;
        }
    }

    private CachedIssue? ReadIssueFromDisk(string issueKey)
    {
        var path = Path.Combine(_folder, IssueFileName(issueKey));

        Interlocked.Increment(ref _readsWaiting);

        lock (_diskLock)
        {
            Interlocked.Decrement(ref _readsWaiting);

            if (IsDisposed() || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = AtomicFile.OpenRead(path);
                var file = JsonSerializer.Deserialize(stream, _issueContext.CachedIssueFile);
                var issue = file is null ? null : IssueCacheMapper.FromFile(file);

                if (issue is not null && string.Equals(issue.Detail.Key, issueKey, StringComparison.OrdinalIgnoreCase))
                {
                    Touch(path);

                    return issue;
                }
            }
            catch (FileNotFoundException)
            {
                // Another instance replaced or removed the file after the existence check: a miss, not a problem.
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ReportReadProblem(exception);

                return null;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
            {
                // Corrupt or foreign content: report it, then fall through to the delete below.
                ReportReadProblem(exception);
            }

            DeleteQuietly(path);

            return null;
        }
    }

    private void WriteAll(Dictionary<CacheKey, CachedView> batch, List<CachedIssue> issues)
    {
        try
        {
            // Under the disk lock, so a disposed cache (a folder about to be deleted) never recreates it.
            WithDisk(() => _permissions.CreateDirectory(_folder));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(exception);

            return;
        }

        TouchUsed();

        foreach (var (key, view) in batch)
        {
            TryWriteView(key, view);
        }

        foreach (var issue in issues)
        {
            WriteIssue(issue);
        }

        EvictQuietly();
    }

    // Eviction goes by file time, so a file read since the last flush is touched: used often means kept.
    private void TouchUsed()
    {
        string[] names;

        lock (_memoryLock)
        {
            names = [.. _used];
            _used.Clear();
        }

        foreach (var name in names)
        {
            WithDisk(() => Touch(Path.Combine(_folder, name)));
        }
    }

    private static void Touch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be touched keeps its old time; at worst it is evicted a little early.
        }
    }

    // One locked or unwritable file does not stop the rest of the batch; the first failure is reported.
    private void TryWriteView(CacheKey key, CachedView view)
    {
        try
        {
            WriteFile(
                Path.Combine(_folder, ViewFileName(key)),
                CacheMapper.ToFile(key, view),
                CacheJsonContext.Default.CachedViewFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(exception);
        }
    }

    private void EvictQuietly()
    {
        try
        {
            EvictOldest();
        }
        catch (FileNotFoundException)
        {
            // Another instance replaced or removed a file between the listing and its stat; the next flush evicts.
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
            // A detail that cannot be serialized is simply not cached (the temporary file is already removed).
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(exception);
        }
    }

    private void WriteFile<TFile>(string path, TFile file, JsonTypeInfo<TFile> typeInfo)
    {
        // Serialized before the lock, and held for this one file only: a cache read waits for at most one write.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, typeInfo);

        YieldToReads();

        lock (_diskLock)
        {
            if (IsDisposed())
            {
                return;
            }

            _file.Write(path, stream => stream.Write(bytes), _permissions.RestrictFile);
            AddWritten(bytes.Length);
        }
    }

    // Before each file step of a flush: a read that is already waiting for the disk lock gets it first. Bounded, so a
    // steady stream of reads cannot hold a flush (the one at exit included) back for long.
    private void YieldToReads()
    {
        for (var attempt = 0; attempt < MaxYields && Volatile.Read(ref _readsWaiting) > 0; attempt++)
        {
            Thread.Sleep(1);
        }
    }

    private void AddWritten(long length)
    {
        if (_diskBytesEstimate >= 0)
        {
            _diskBytesEstimate += length;
        }
    }

    /// <summary>
    /// Deletes the oldest files until the folder fits the size cap. The folder is listed only when the bytes written
    /// since the last listing could have pushed it over the cap, so most flushes cost no directory scan.
    /// </summary>
    private void EvictOldest()
    {
        lock (_diskLock)
        {
            if (IsDisposed() || (_diskBytesEstimate >= 0 && _diskBytesEstimate <= _maxDiskBytes))
            {
                return;
            }

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

            _diskBytesEstimate = total;
        }
    }

    private void ReportReadProblem(Exception exception)
    {
        lock (_memoryLock)
        {
            if (_readProblemReported)
            {
                return;
            }

            _readProblemReported = true;
        }

        ReadProblem?.Invoke(
            $"A local cache file could not be read ({exception.GetType().Name}); the data is loaded from Jira again.");
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
}
