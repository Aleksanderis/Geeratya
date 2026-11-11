using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Storage;
using Geeratya.Core.Tests.Fakes;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Cache;

public sealed class SiteCacheTests : IDisposable
{
    private static readonly DateTimeOffset _saved = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly CacheKey _key = new(ViewSourceKind.Jql, "project = SECRET ORDER BY created");

    private SiteCache? _shared;
    private CacheKey[] _sharedKeys = [];
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
        using var permissions = new RecordingFilePermissions();
        using var cache = new SiteCache(folder, permissions, TimeSpan.FromMilliseconds(20));

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));

        Assert.True(permissions.Restricted.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void GivenMoreThanOnePageOfRows_WhenPut_ThenOnlyOnePageIsKept()
    {
        using var cache = new SiteCache(Path.Combine(_root, "p"));
        var rows = Enumerable.Range(1, ViewPaging.PageSize + 5).Select(number => Issue(number)).ToArray();

        cache.Put(_key, new CachedView(rows, null, _saved));

        Assert.Equal(ViewPaging.PageSize, cache.Get(_key)!.Rows.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GivenOldSchemaFile_WhenReading_ThenItIsIgnoredAndDeleted(int schema)
    {
        var folder = Path.Combine(_root, "p");
        var path = Path.Combine(folder, SiteCache.ViewFileName(_key));
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, $$"""{"schema":{{schema}},"source":"Jql","sourceValue":"x","savedAt":"2026-01-01T00:00:00Z","rows":[]}""");
        using var cache = new SiteCache(folder);

        Assert.Null(cache.Get(_key));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"schema":3}""")]
    [InlineData("""{"schema":3,"source":"Jql","sourceValue":"q","savedAt":"2026-01-01T00:00:00Z","rows":[{"id":"1","key":"A-1","summary":"s","status":null,"issueType":null,"labels":null,"projectKey":"A"}]}""")]
    [InlineData("""{"schema":3,"source":"Jql","sourceValue":"q","savedAt":"2026-01-01T00:00:00Z","rows":[{"id":"1","key":"A-1","summary":"s","status":{"id":"1","name":"n","categoryKey":"new"},"issueType":{"id":"1","name":"t","hierarchyLevel":0,"isSubtask":false},"labels":[],"projectKey":"A","sprints":null}]}""")]
    [InlineData("""{"schema":3,"source":"Jql","sourceValue":"q","savedAt":"2026-01-01T00:00:00Z","rows":[{"id":"1","key":"A-1","summary":"s","status":{"id":"1","name":"n","categoryKey":"new"},"issueType":{"id":"1","name":"t","hierarchyLevel":0,"isSubtask":false},"labels":[],"projectKey":"A","sprints":[null]}]}""")]
    [InlineData("""{"schema":3,"source":"5","sourceValue":"q","savedAt":"2026-01-01T00:00:00Z","rows":[]}""")]
    [InlineData("""{"schema":3,"source":"Jql","sourceValue":null,"savedAt":"2026-01-01T00:00:00Z","rows":null}""")]
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
    public void GivenTwoCorruptFiles_WhenReading_ThenOneReadProblemIsRaised()
    {
        var folder = Path.Combine(_root, "p");
        var other = new CacheKey(ViewSourceKind.Jql, "project = OTHER");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, SiteCache.ViewFileName(_key)), "not json at all");
        File.WriteAllText(Path.Combine(folder, SiteCache.ViewFileName(other)), "not json at all");
        var notices = new List<string>();
        using var cache = new SiteCache(folder);
        cache.ReadProblem += notices.Add;

        Assert.Null(cache.Get(_key));
        Assert.Null(cache.Get(other));

        Assert.Single(notices);
        Assert.Equal(
            "A local cache file could not be read (JsonException); the data is loaded from Jira again.",
            notices[0]);
    }

    [Fact]
    public void GivenMissingFile_WhenReading_ThenNoReadProblemIsRaised()
    {
        var notices = new List<string>();
        using var cache = new SiteCache(Path.Combine(_root, "p"));
        cache.ReadProblem += notices.Add;

        Assert.Null(cache.Get(_key));

        Assert.Empty(notices);
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
        _shared = new SiteCache(folder);
        var keys = Enumerable.Range(0, 20).Select(number => new CacheKey(ViewSourceKind.Jql, $"q{number}")).ToArray();
        _sharedKeys = keys;

        Parallel.For(0, 200, PutAndSometimesFlush);
        _shared.Flush();

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
        using var permissions = new RecordingFilePermissions();
        using var cache = new SiteCache(folder, permissions);

        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Flush();

        Assert.Equal([folder], permissions.Folders);
        Assert.StartsWith(folder, Assert.Single(permissions.Files), StringComparison.Ordinal);
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
    public void GivenOneViewFileStaysLocked_WhenFlushed_ThenTheOtherEntriesOfTheBatchAreStillWritten()
    {
        var folder = Path.Combine(_root, "p");
        var lockedKey = new CacheKey(ViewSourceKind.Jql, "locked");
        var otherKey = new CacheKey(ViewSourceKind.Jql, "other");
        var lockedName = SiteCache.ViewFileName(lockedKey);
        var replacer = new LockedFileReplacer(
            target => Path.GetFileName(target) == lockedName,
            int.MaxValue);
        var notices = new List<string>();

        using (var cache = new SiteCache(folder, file: new AtomicFile(replacer)))
        {
            cache.WriteFailed += notices.Add;
            cache.Put(lockedKey, new CachedView([Issue(1)], null, _saved));
            cache.Put(otherKey, new CachedView([Issue(2)], null, _saved));
            cache.Flush();
        }

        using var fresh = new SiteCache(folder);
        Assert.Equal("A-2", fresh.Get(otherKey)!.Rows[0].Key);
        Assert.Null(fresh.Get(lockedKey));
        Assert.Single(notices);
        Assert.DoesNotContain(Directory.GetFiles(folder), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAViewFileIsBrieflyLocked_WhenFlushed_ThenItIsWrittenAfterARetry()
    {
        var folder = Path.Combine(_root, "p");
        var replacer = new LockedFileReplacer(_ => true, failures: 1);
        var notices = new List<string>();

        using (var cache = new SiteCache(folder, file: new AtomicFile(replacer)))
        {
            cache.WriteFailed += notices.Add;
            cache.Put(_key, new CachedView([Issue(1)], null, _saved));
            cache.Flush();
        }

        using var fresh = new SiteCache(folder);
        Assert.Equal("A-1", fresh.Get(_key)!.Rows[0].Key);
        Assert.Empty(notices);
    }

    [Fact]
    public void GivenTwoCachesOnOneFolder_WhenBothFlushTheSameView_ThenNeitherWriteFails()
    {
        var folder = Path.Combine(_root, "p");
        var notices = new List<string>();
        using var first = new SiteCache(folder);
        using var second = new SiteCache(folder);
        first.WriteFailed += notices.Add;
        second.WriteFailed += notices.Add;

        FlushBoth(first, second);

        Assert.Empty(notices);
        Assert.Single(Directory.GetFiles(folder));
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

    [Fact]
    public void GivenFlushedViews_WhenViewsAreRemoved_ThenTheyMissAtOnceAndTheFilesGoWithTheNextFlush()
    {
        var folder = Path.Combine(_root, "p");
        var other = new CacheKey(ViewSourceKind.SavedFilter, "10001");
        using var cache = new SiteCache(folder);
        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Put(other, new CachedView([Issue(2)], null, _saved));
        cache.Flush();

        cache.RemoveViews();

        Assert.Null(cache.Get(_key));
        Assert.Null(cache.Get(other));
        Assert.True(File.Exists(Path.Combine(folder, SiteCache.ViewFileName(_key))));

        cache.Flush();

        Assert.False(File.Exists(Path.Combine(folder, SiteCache.ViewFileName(_key))));
        Assert.False(File.Exists(Path.Combine(folder, SiteCache.ViewFileName(other))));
        using var reader = new SiteCache(folder);
        Assert.Null(reader.Get(_key));
    }

    [Fact]
    public void GivenRemovedViews_WhenOneIsCachedAgainBeforeTheFlush_ThenOnlyTheNewOneSurvives()
    {
        var folder = Path.Combine(_root, "p");
        var other = new CacheKey(ViewSourceKind.SavedFilter, "10001");
        using var cache = new SiteCache(folder);
        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Put(other, new CachedView([Issue(2)], null, _saved));
        cache.Flush();

        cache.RemoveViews();
        cache.Put(_key, new CachedView([Issue(3)], null, _saved));
        cache.Flush();

        using var reader = new SiteCache(folder);
        Assert.Equal(["A-3"], reader.Get(_key)!.Rows.Select(row => row.Key));
        Assert.Null(reader.Get(other));
    }


    public void Dispose()
    {
        _shared?.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static void FlushBoth(SiteCache first, SiteCache second)
    {
        Parallel.Invoke(
            () => PutAndFlush(first, 1),
            () => PutAndFlush(second, 2));
    }

    private static void PutAndFlush(SiteCache cache, int number)
    {
        for (var round = 0; round < 20; round++)
        {
            cache.Put(_key, new CachedView([Issue(number)], null, _saved));
            cache.Flush();
        }
    }

    private void PutAndSometimesFlush(int step)
    {
        _shared!.Put(_sharedKeys[step % _sharedKeys.Length], new CachedView([Issue(step)], null, _saved));

        if (step % 7 == 0)
        {
            _shared.Flush();
        }
    }

    [Fact]
    public void GivenScrumRowsWithSprintsEstimateAndEpic_WhenReadByANewCache_ThenTheyRoundTrip()
    {
        var folder = Path.Combine(_root, "p");
        var row = Issue(1, true) with { Parent = new IssueParent("A-100", "Epic", true) };
        var scrum = new IssueSummary(
            row.Id, row.Key, row.Summary, row.Status, row.IssueType, row.Priority, row.Assignee, row.Labels,
            row.Parent, row.Updated, row.ProjectKey)
        {
            Sprints =
            [
                new IssueSprint(7, "Sprint 7", SprintState.Closed),
                new IssueSprint(8, "Sprint 8", SprintState.Active),
            ],
            Estimate = 2.5,
        };
        using (var writer = new SiteCache(folder))
        {
            writer.Put(_key, new CachedView([scrum], null, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.Get(_key)!.Rows[0];

        Assert.Equal(scrum.Sprints, read.Sprints);
        Assert.Equal("Sprint 8", read.CurrentSprint!.Name);
        Assert.Equal(2.5, read.Estimate);
        Assert.True(read.Parent!.IsEpic);
        Assert.Equal("A-100", read.Epic!.Key);
    }

    [Fact]
    public void GivenRowWithoutSprintsOrEstimate_WhenReadByANewCache_ThenNoneAppear()
    {
        var folder = Path.Combine(_root, "p");
        using (var writer = new SiteCache(folder))
        {
            writer.Put(_key, new CachedView([Issue(1, true)], null, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.Get(_key)!.Rows[0];

        Assert.Empty(read.Sprints);
        Assert.Null(read.Estimate);
        Assert.Null(read.Epic);
    }

    [Fact]
    public void GivenBoardWithEstimationRead_WhenCachedAndRead_ThenTheNameSurvivesAndNothingIsHidden()
    {
        var folder = Path.Combine(_root, "p");
        var board = new BoardConfiguration(4, "Board", BoardType.Scrum, [], "10000", null, null, null)
        {
            EstimationRead = true,
            EstimationFieldId = "customfield_10047",
            EstimationFieldName = "Story Points",
        };
        using (var writer = new SiteCache(folder))
        {
            writer.Put(_key, new CachedView([Issue(1)], board, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.Get(_key)!.Board!;

        Assert.Equal("Story Points", read.EstimationFieldName);
        Assert.False(read.EstimationRead);
        Assert.False(read.HasNoEstimation);
    }

    [Fact]
    public void GivenCacheUnderItsCap_WhenFlushedAgain_ThenTheFolderIsNotListedForEviction()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder, maxDiskBytes: 10 * 1024 * 1024);
        cache.Put(_key, new CachedView([Issue(1)], null, _saved));
        cache.Flush();
        var stray = Path.Combine(folder, "view-old.json");
        File.WriteAllText(stray, new string('x', 100));
        File.SetLastWriteTimeUtc(stray, DateTime.UtcNow.AddDays(-30));

        cache.Put(_key, new CachedView([Issue(2)], null, _saved));
        cache.Flush();

        Assert.True(File.Exists(stray));
    }

    [Fact]
    public void GivenCacheOverItsCap_WhenFlushed_ThenTheLeastRecentlyUsedFileGoes()
    {
        var folder = Path.Combine(_root, "p");
        var oftenRead = new CacheKey(ViewSourceKind.Jql, "often read");
        var neverRead = new CacheKey(ViewSourceKind.Jql, "never read");

        using (var seed = new SiteCache(folder))
        {
            seed.Put(oftenRead, new CachedView([Issue(1)], null, _saved));
            seed.Put(neverRead, new CachedView([Issue(2)], null, _saved));
            seed.Flush();
        }

        var oftenPath = Path.Combine(folder, SiteCache.ViewFileName(oftenRead));
        var neverPath = Path.Combine(folder, SiteCache.ViewFileName(neverRead));
        File.SetLastWriteTimeUtc(oftenPath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(neverPath, DateTime.UtcNow.AddDays(-1));
        var cap = new FileInfo(oftenPath).Length + new FileInfo(neverPath).Length + 10;

        using var cache = new SiteCache(folder, maxDiskBytes: cap);
        Assert.NotNull(cache.Get(oftenRead));
        cache.Put(new CacheKey(ViewSourceKind.Jql, "third"), new CachedView([Issue(3)], null, _saved));
        cache.Flush();

        Assert.True(File.Exists(oftenPath));
        Assert.False(File.Exists(neverPath));
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
