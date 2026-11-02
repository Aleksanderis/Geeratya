using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Cache;

public sealed class SiteCacheTests : IDisposable
{
    private static readonly DateTimeOffset _saved = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly CacheKey _key = new(ViewSourceKind.Jql, "project = SECRET ORDER BY created");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"geeratya-cache-{Guid.NewGuid():N}");

    [Fact]
    public void GivenFlushedView_WhenReadByANewCache_ThenEveryFieldRoundTrips()
    {
        var folder = Path.Combine(_root, "p");
        var board = new BoardConfiguration(
            4,
            "Board",
            BoardType.Kanban,
            [new BoardColumn("To Do", ["1", "2"])],
            "10000",
            "assignee is not EMPTY",
            "customfield_10019",
            "issueCount");
        var view = new CachedView([Issue(1, full: true), Issue(2)], board, _saved);
        using (var writer = new SiteCache(folder))
        {
            writer.Put(_key, view);
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.Get(_key);

        Assert.NotNull(read);
        Assert.Equal(_saved, read.SavedAt);
        var first = read.Rows[0];
        Assert.Equal(["A-1", "A-2"], read.Rows.Select(row => row.Key));
        Assert.Equal(new IssueStatus("1", "To Do", "new"), first.Status);
        Assert.Equal(new IssueTypeSummary("10", "Task", 0, false), first.IssueType);
        Assert.Equal("High", first.Priority);
        Assert.Equal(new IssueAssignee("acc-1", "Test User 1"), first.Assignee);
        Assert.Equal(["bug", "ui"], first.Labels);
        Assert.Equal(new IssueParent("A-100", "Epic"), first.Parent);
        Assert.Equal(_saved, first.Updated);
        Assert.Null(read.Rows[1].Assignee);
        Assert.Null(read.Rows[1].Parent);
        Assert.Equal(board.Columns[0].StatusIds, read.Board!.Columns[0].StatusIds);
        Assert.Equal(board with { Columns = [] }, read.Board with { Columns = [] });
    }

    [Fact]
    public void GivenPutWithoutFlush_WhenLookingAtTheDisk_ThenNothingIsWrittenYetButTheMemoryServesIt()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));

        Assert.False(Directory.Exists(folder));
        Assert.Single(cache.Get(_key)!.Rows);
    }

    [Fact]
    public void GivenDebounce_WhenTheTimerFires_ThenThePendingViewIsWrittenOffTheCallingThread()
    {
        var folder = Path.Combine(_root, "p");
        var written = new ManualResetEventSlim();
        var permissions = new FilePermissions(
            path => Directory.CreateDirectory(path),
            _ => written.Set());
        using var cache = new SiteCache(folder, permissions, TimeSpan.FromMilliseconds(20));

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));

        Assert.True(written.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void GivenMoreThanOnePageOfRows_WhenPut_ThenOnlyOnePageIsKept()
    {
        using var cache = new SiteCache(Path.Combine(_root, "p"));
        var rows = Enumerable.Range(1, ViewPaging.PageSize + 5).Select(number => Issue(number)).ToArray();

        cache.Put(_key, new CachedView(rows, null, _saved));

        Assert.Equal(ViewPaging.PageSize, cache.Get(_key)!.Rows.Count);
    }

    [Fact]
    public void GivenOldSchemaFile_WhenReading_ThenItIsIgnoredAndDeleted()
    {
        var folder = Path.Combine(_root, "p");
        var path = Path.Combine(folder, SiteCache.ViewFileName(_key));
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, """{"schema":0,"source":"Jql","sourceValue":"x","savedAt":"2026-01-01T00:00:00Z","rows":[]}""");
        using var cache = new SiteCache(folder);

        Assert.Null(cache.Get(_key));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"schema":1}""")]
    public void GivenCorruptFile_WhenReading_ThenItIsIgnoredAndDeleted(string content)
    {
        var folder = Path.Combine(_root, "p");
        var path = Path.Combine(folder, SiteCache.ViewFileName(_key));
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, content);
        using var cache = new SiteCache(folder);

        Assert.Null(cache.Get(_key));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void GivenFileHoldingAnotherKey_WhenReading_ThenItIsIgnoredAndDeleted()
    {
        var folder = Path.Combine(_root, "p");
        var other = new CacheKey(ViewSourceKind.Jql, "project = OTHER");
        using (var writer = new SiteCache(folder))
        {
            writer.Put(other, new CachedView([Issue(1)], null, _saved));
            writer.Flush();
        }

        File.Move(Path.Combine(folder, SiteCache.ViewFileName(other)), Path.Combine(folder, SiteCache.ViewFileName(_key)));
        using var cache = new SiteCache(folder);

        Assert.Null(cache.Get(_key));
        Assert.Empty(Directory.GetFiles(folder));
    }

    [Fact]
    public void GivenManyThreadsPutting_WhenFlushed_ThenEveryFileIsCompleteAndReadable()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);
        var keys = Enumerable.Range(0, 20).Select(number => new CacheKey(ViewSourceKind.Jql, $"q{number}")).ToArray();

        Parallel.For(0, 200, step =>
        {
            cache.Put(keys[step % keys.Length], new CachedView([Issue(step)], null, _saved));

            if (step % 7 == 0)
            {
                cache.Flush();
            }
        });
        cache.Flush();

        using var fresh = new SiteCache(folder);
        Assert.All(keys, key => Assert.Single(fresh.Get(key)!.Rows));
        Assert.DoesNotContain(Directory.GetFiles(folder), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenDiskCap_WhenViewsExceedIt_ThenTheOldestFilesAreEvictedFirst()
    {
        var folder = Path.Combine(_root, "p");
        var rows = Enumerable.Range(1, 20).Select(number => Issue(number)).ToArray();
        var oldKey = new CacheKey(ViewSourceKind.Jql, "old");
        var newKey = new CacheKey(ViewSourceKind.Jql, "new");
        using var probe = new SiteCache(folder);
        probe.Put(oldKey, new CachedView(rows, null, _saved));
        probe.Flush();

        var oldPath = Path.Combine(folder, SiteCache.ViewFileName(oldKey));
        var size = new FileInfo(oldPath).Length;
        File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddDays(-1));

        using var cache = new SiteCache(folder, maxDiskBytes: size + (size / 2));
        cache.Put(newKey, new CachedView(rows, null, _saved));
        cache.Flush();

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(Path.Combine(folder, SiteCache.ViewFileName(newKey))));
    }

    [Fact]
    public void GivenMoreThanTheMemoryBound_WhenReading_ThenTheLeastRecentlyUsedViewLeavesMemory()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);
        var keys = Enumerable.Range(0, SiteCache.MaxViews + 1).Select(number => new CacheKey(ViewSourceKind.Jql, $"q{number}")).ToArray();

        foreach (var key in keys)
        {
            cache.Put(key, new CachedView([Issue(1)], null, _saved));
            cache.Flush();
        }

        foreach (var file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }

        Assert.Null(cache.Get(keys[0]));
        Assert.NotNull(cache.Get(keys[^1]));
    }

    [Fact]
    public void GivenFileNames_WhenWritten_ThenTheyHoldNeitherTheQueryNorAnyIdentity()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Flush();

        var name = Path.GetFileName(Assert.Single(Directory.GetFiles(folder)));
        Assert.DoesNotContain("SECRET", name, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("view-", name, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFolderAndFiles_WhenWriting_ThenThePermissionHooksRunForBoth()
    {
        var folder = Path.Combine(_root, "p");
        var folders = new List<string>();
        var files = new List<string>();
        var permissions = new FilePermissions(
            path =>
            {
                folders.Add(path);
                Directory.CreateDirectory(path);
            },
            files.Add);
        using var cache = new SiteCache(folder, permissions);

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Flush();

        Assert.Equal([folder], folders);
        Assert.StartsWith(folder, Assert.Single(files), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenUnwritableFolder_WhenFlushed_ThenOneNoticeIsRaisedAndTheMemoryStillServes()
    {
        Directory.CreateDirectory(_root);

        var blocker = Path.Combine(_root, "file");
        File.WriteAllText(blocker, "x");
        var notices = new List<string>();
        using var cache = new SiteCache(Path.Combine(blocker, "inner"));
        cache.WriteFailed += notices.Add;

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Flush();
        cache.Put(_key, new CachedView([Issue(2)], null, _saved));
        cache.Flush();

        Assert.Single(notices);
        Assert.DoesNotContain("Bearer", notices[0], StringComparison.Ordinal);
        Assert.Equal("A-2", cache.Get(_key)!.Rows[0].Key);
    }

    [Fact]
    public void GivenTwoProfilesOnOneSite_WhenCaching_ThenTheyUseDifferentFoldersAndNeverSeeEachOther()
    {
        using var caches = new SiteCaches(_root);
        var mine = new Profile("mine", "https://x.atlassian.net", "me@x.test");
        var colleague = new Profile("other", "https://x.atlassian.net", "you@x.test");

        caches.For(mine).Put(_key, new CachedView([Issue(1)], null, _saved));
        caches.FlushAll();

        Assert.NotEqual(caches.FolderOf(mine), caches.FolderOf(colleague));
        Assert.Null(caches.For(colleague).Get(_key));
        Assert.NotNull(caches.For(mine).Get(_key));
    }

    [Fact]
    public void GivenProfileFolder_WhenNamed_ThenItHoldsOnlyHexAndNoSiteOrEmail()
    {
        using var caches = new SiteCaches(_root);
        var profile = new Profile("mine", "https://x.atlassian.net", "me@x.test");

        var name = Path.GetFileName(caches.FolderOf(profile));

        Assert.Equal(16, name.Length);
        Assert.All(name, character => Assert.True(Uri.IsHexDigit(character)));
    }

    [Fact]
    public void GivenSwitch_WhenDropping_ThenMemoryGoesAndTheDiskFilesStay()
    {
        using var caches = new SiteCaches(_root);
        var profile = new Profile("mine", "https://x.atlassian.net", "me@x.test");
        caches.For(profile).Put(_key, new CachedView([Issue(1)], null, _saved));
        caches.FlushAll();
        var old = caches.For(profile);

        caches.Drop(profile);

        Assert.Null(old.Get(_key));
        Assert.True(Directory.Exists(caches.FolderOf(profile)));
        Assert.NotNull(caches.For(profile).Get(_key));
    }

    [Fact]
    public void GivenTwoProfiles_WhenDeletingOne_ThenOnlyItsFolderIsRemoved()
    {
        using var caches = new SiteCaches(_root);
        var gone = new Profile("a", "https://a.atlassian.net", "me@a.test");
        var kept = new Profile("b", "https://b.atlassian.net", "me@b.test");
        caches.For(gone).Put(_key, new CachedView([Issue(1)], null, _saved));
        caches.For(kept).Put(_key, new CachedView([Issue(1)], null, _saved));
        caches.FlushAll();

        var deleted = caches.Delete(gone);

        Assert.True(deleted);
        Assert.False(Directory.Exists(caches.FolderOf(gone)));
        Assert.True(Directory.Exists(caches.FolderOf(kept)));
    }

    [Fact]
    public void GivenPendingWrite_WhenTheProfileIsDeleted_ThenTheFolderIsNotRecreated()
    {
        using var caches = new SiteCaches(_root);
        var profile = new Profile("a", "https://a.atlassian.net", "me@a.test");
        var cache = caches.For(profile);

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        caches.Delete(profile);
        cache.Flush();
        cache.Put(_key, new CachedView([Issue(2)], null, _saved));
        cache.Flush();

        Assert.False(Directory.Exists(caches.FolderOf(profile)));
        Assert.Null(cache.Get(_key));
    }

    [Fact]
    public void GivenNeverCachedProfile_WhenDeleting_ThenItSucceeds()
    {
        using var caches = new SiteCaches(_root);

        Assert.True(caches.Delete(new Profile("a", "https://a.atlassian.net", "me@a.test")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static IssueSummary Issue(int number, bool full = false)
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary(
            $"{number}",
            $"A-{number}",
            $"Issue {number}",
            status,
            type,
            full ? "High" : null,
            full ? new IssueAssignee("acc-1", "Test User 1") : null,
            full ? ["bug", "ui"] : [],
            full ? new IssueParent("A-100", "Epic") : null,
            full ? _saved : null,
            "A");
    }
}
