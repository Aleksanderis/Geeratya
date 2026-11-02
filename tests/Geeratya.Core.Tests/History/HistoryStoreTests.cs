using Geeratya.Core.History;
using Geeratya.Core.Profiles;

namespace Geeratya.Core.Tests.History;

public sealed class HistoryStoreTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-history-{Guid.NewGuid():N}");

    [Fact]
    public void GivenNoFile_WhenLoading_ThenTheHistoryIsEmpty()
    {
        var store = new HistoryStore(_directory);

        Assert.Empty(store.Load(_work).Queries);
    }

    [Fact]
    public void GivenAnUpdate_WhenLoadingFromANewStore_ThenTheEntriesSurvive()
    {
        new HistoryStore(_directory).Update(_work, history => history.WithQuery("project = A").WithIssue("A-1"));

        var loaded = new HistoryStore(_directory).Load(_work);

        Assert.Equal(["project = A"], loaded.Queries);
        Assert.Equal(["A-1"], loaded.Issues);
    }

    [Fact]
    public void GivenTwoProfiles_WhenOneRecords_ThenTheOtherSeesNothing()
    {
        var store = new HistoryStore(_directory);

        store.Update(_work, history => history.WithQuery("project = WORK"));

        Assert.Empty(store.Load(_home).Queries);
        Assert.NotEqual(store.FolderOf(_work), store.FolderOf(_home));
    }

    [Fact]
    public void GivenAProfile_WhenSaved_ThenNoSiteOrAccountAppearsInThePath()
    {
        var store = new HistoryStore(_directory);

        store.Update(_work, history => history.WithQuery("x = 1"));

        var path = store.FolderOf(_work);
        Assert.DoesNotContain("atlassian", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("me@work", path, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(path, HistoryStore.FileName)));
    }

    [Fact]
    public void GivenACorruptFile_WhenLoading_ThenTheHistoryIsEmptyAndTheNextUpdateRepairsIt()
    {
        var store = new HistoryStore(_directory);

        Directory.CreateDirectory(store.FolderOf(_work));
        File.WriteAllText(Path.Combine(store.FolderOf(_work), HistoryStore.FileName), "{ not json");

        Assert.Empty(store.Load(_work).Queries);
        Assert.Equal(["a = 1"], store.Update(_work, history => history.WithQuery("a = 1")).Queries);
    }

    [Fact]
    public void GivenHistory_WhenDeleted_ThenOnlyThatProfileIsForgottenAndTheFolderGoes()
    {
        var store = new HistoryStore(_directory);

        store.Update(_work, history => history.WithQuery("w = 1"));
        store.Update(_home, history => history.WithQuery("h = 1"));

        store.Delete(_work);

        Assert.Empty(store.Load(_work).Queries);
        Assert.False(Directory.Exists(store.FolderOf(_work)));
        Assert.Equal(["h = 1"], store.Load(_home).Queries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
