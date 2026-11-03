using Geeratya.Core.Profiles;
using Geeratya.Core.Startup;

namespace Geeratya.Core.Tests.Startup;

public sealed class RememberedTabsStoreTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-tabs-{Guid.NewGuid():N}");

    [Fact]
    public void GivenNoFile_WhenLoading_ThenTheListIsEmpty()
    {
        Assert.Empty(new RememberedTabsStore(_directory).Load(_work).Items);
    }

    [Fact]
    public void GivenSavedTabs_WhenLoadingFromANewStore_ThenDefinitionsAndTheActiveIndexRoundTrip()
    {
        var tabs = new RememberedTabs(
            [
                new RememberedTab("Jql", "project = A", "List", "A"),
                new RememberedTab("Board", "4", "Columns", "GCM board", [7L], "Assignee", "alice"),
            ],
            1);

        new RememberedTabsStore(_directory).Save(_work, tabs);

        var loaded = new RememberedTabsStore(_directory).Load(_work);
        Assert.True(tabs.SameAs(loaded));
    }

    [Fact]
    public void GivenTwoProfiles_WhenSavingOne_ThenTheOtherStaysEmpty()
    {
        var store = new RememberedTabsStore(_directory);

        store.Save(_work, new RememberedTabs([new RememberedTab("Jql", "a = 1", "List", "A")]));

        Assert.Empty(store.Load(_home).Items);
        Assert.NotEqual(store.FolderOf(_work), store.FolderOf(_home));
    }

    [Fact]
    public void GivenACorruptFile_WhenLoading_ThenItIsEmptyAndTheFileIsKeptAsBad()
    {
        var store = new RememberedTabsStore(_directory);
        var folder = store.FolderOf(_work);

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, RememberedTabsStore.FileName), "{ not json");

        Assert.Empty(store.Load(_work).Items);
        Assert.True(File.Exists(Path.Combine(folder, $"{RememberedTabsStore.FileName}.bad")));
        Assert.False(File.Exists(Path.Combine(folder, RememberedTabsStore.FileName)));
    }

    [Fact]
    public void GivenAFileWithNullEntriesAndUnknownKinds_WhenLoading_ThenOnlyUsableTabsRemain()
    {
        var store = new RememberedTabsStore(_directory);
        var folder = store.FolderOf(_work);

        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, RememberedTabsStore.FileName),
            """{"items":[null,{"source":"Hologram","sourceValue":"x","layout":"List","title":"T"},{"source":"Jql","sourceValue":"a = 1","layout":"List","title":"A"}],"active":2}""");

        var loaded = store.Load(_work);

        Assert.Equal("A", Assert.Single(loaded.Items).Title);
        Assert.Equal(0, loaded.Active);
    }

    [Fact]
    public void GivenSavedTabs_WhenReadingTheFile_ThenItHoldsOnlyDefinitions()
    {
        var store = new RememberedTabsStore(_directory);

        store.Save(_work, new RememberedTabs([new RememberedTab("Jql", "a = 1", "List", "A")]));

        var text = File.ReadAllText(Path.Combine(store.FolderOf(_work), RememberedTabsStore.FileName));
        Assert.DoesNotContain("atlassian", text);
        Assert.DoesNotContain("me@work.test", text);
        Assert.False(File.Exists(Path.Combine(store.FolderOf(_work), $"{RememberedTabsStore.FileName}.tmp")));
    }

    [Fact]
    public void GivenSavedTabs_WhenDeleting_ThenTheFolderGoes()
    {
        var store = new RememberedTabsStore(_directory);

        store.Save(_work, new RememberedTabs([new RememberedTab("Jql", "a = 1", "List", "A")]));
        store.Delete(_work);

        Assert.False(Directory.Exists(store.FolderOf(_work)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
