using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.SavedViews;

public sealed class SavedViewStoreTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-views-{Guid.NewGuid():N}");

    [Fact]
    public void GivenNoFile_WhenLoading_ThenTheListIsEmpty()
    {
        Assert.Empty(new SavedViewStore(_directory).Load(_work).Items);
    }

    [Fact]
    public void GivenASavedView_WhenLoadingFromANewStore_ThenSourceAndLayoutRoundTrip()
    {
        var view = SavedViewList.FromDefinition(
            "Open bugs",
            new ViewDefinition(ViewSourceKind.Jql, "type = Bug", ViewLayout.List, "ignored"));

        new SavedViewStore(_directory).Update(_work, views => views.Saved(view));

        var loaded = Assert.Single(new SavedViewStore(_directory).Load(_work).Items);
        var definition = SavedViewList.ToDefinition(loaded)!;
        Assert.Equal("Open bugs", definition.Title);
        Assert.Equal(ViewSourceKind.Jql, definition.Source);
        Assert.Equal("type = Bug", definition.SourceValue);
        Assert.Equal(ViewLayout.List, definition.Layout);
    }

    [Fact]
    public void GivenAColumnsView_WhenReloaded_ThenTheLayoutIsKept()
    {
        var store = new SavedViewStore(_directory);
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");

        store.Update(_work, views => views.Saved(SavedViewList.FromDefinition("Team board", definition)));

        var loaded = SavedViewList.ToDefinition(store.Load(_work).Items[0])!;
        Assert.Equal(ViewLayout.Columns, loaded.Layout);
        Assert.Equal(ViewSourceKind.Board, loaded.Source);
    }

    [Fact]
    public void GivenTwoProfiles_WhenOneSaves_ThenTheOtherSeesNothing()
    {
        var store = new SavedViewStore(_directory);

        store.Update(_work, views => views.Saved(new SavedView("Mine", "Jql", "a = 1", "List")));

        Assert.Empty(store.Load(_home).Items);
        Assert.NotEqual(store.FolderOf(_work), store.FolderOf(_home));
    }

    [Fact]
    public void GivenAFileWithUnknownFields_WhenLoading_ThenTheyAreIgnoredAndKnownOnesKept()
    {
        var json = """
            { "items": [ { "name": "A", "source": "Jql", "sourceValue": "x = 1", "layout": "List", "future": 7 } ],
              "schema": 2 }
            """;

        var store = WriteFile(json);

        Assert.Equal("A", Assert.Single(store.Load(_work).Items).Name);
    }

    [Fact]
    public void GivenAnEntryWithAnUnknownSourceOrBlankName_WhenLoading_ThenOnlyThatEntryIsSkipped()
    {
        var json = """
            { "items": [
              { "name": "New kind", "source": "Hologram", "sourceValue": "1", "layout": "List" },
              { "name": "  ", "source": "Jql", "sourceValue": "a = 1", "layout": "List" },
              null,
              { "name": "Fine", "source": "Backlog", "sourceValue": "4", "layout": "List" } ] }
            """;

        var store = WriteFile(json);

        Assert.Equal("Fine", Assert.Single(store.Load(_work).Items).Name);
    }

    [Fact]
    public void GivenACorruptFile_WhenLoading_ThenTheListIsEmptyAndTheNextSaveReplacesIt()
    {
        var store = WriteFile("{ not json");

        Assert.Empty(store.Load(_work).Items);

        store.Update(_work, views => views.Saved(new SavedView("A", "Jql", "a = 1", "List")));

        Assert.Single(new SavedViewStore(_directory).Load(_work).Items);
    }

    [Fact]
    public void GivenASave_WhenWriting_ThenNoTemporaryFileRemainsAndNoSiteOrAccountIsStored()
    {
        var store = new SavedViewStore(_directory);

        store.Update(_work, views => views.Saved(new SavedView("A", "Jql", "a = 1", "List")));

        var files = Directory.GetFiles(store.FolderOf(_work));
        var text = File.ReadAllText(files[0]);
        Assert.Equal([SavedViewStore.FileName], files.Select(Path.GetFileName));
        Assert.DoesNotContain("atlassian", text, StringComparison.Ordinal);
        Assert.DoesNotContain("me@work.test", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFile_WhenDeletingTheProfile_ThenFileAndFolderAreGone()
    {
        var store = new SavedViewStore(_directory);

        store.Update(_work, views => views.Saved(new SavedView("A", "Jql", "a = 1", "List")));
        store.Delete(_work);

        Assert.False(Directory.Exists(store.FolderOf(_work)));
    }

    [Fact]
    public void GivenSavedViews_WhenRenamingAndRemoving_ThenTheStoredListFollows()
    {
        var store = new SavedViewStore(_directory);
        var first = new SavedView("A", "Jql", "a = 1", "List");
        var second = new SavedView("B", "Jql", "b = 1", "List");

        store.Update(_work, views => views.Saved(first).Saved(second));
        store.Update(_work, views => views.Renamed("a", "C"));
        store.Update(_work, views => views.Removed("B"));

        Assert.Equal(["C"], store.Load(_work).Items.Select(item => item.Name));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private SavedViewStore WriteFile(string json)
    {
        var store = new SavedViewStore(_directory);

        Directory.CreateDirectory(store.FolderOf(_work));
        File.WriteAllText(Path.Combine(store.FolderOf(_work), SavedViewStore.FileName), json);

        return store;
    }
}
