using Geeratya.Core.BoardKinds;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.BoardKinds;

public sealed class BoardKindStoreTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-kinds-{Guid.NewGuid():N}");

    [Fact]
    public void GivenSavedAnswers_WhenLoadingFromANewStore_ThenTheyArePresent()
    {
        new BoardKindStore(_directory).Save(_work, 7, BoardKind.Scrum);
        new BoardKindStore(_directory).Save(_work, 8, BoardKind.Kanban);

        var loaded = new BoardKindStore(_directory).Load(_work);

        Assert.Equal(BoardKind.Scrum, loaded[7]);
        Assert.Equal(BoardKind.Kanban, loaded[8]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"boards":{"x":"scrum","9":"other","10":"kanban"}}""")]
    public void GivenCorruptOrUnknownContent_WhenLoading_ThenOnlyValidAnswersAreKept(string content)
    {
        var store = new BoardKindStore(_directory);

        store.Save(_work, 1, BoardKind.Scrum);

        var path = Directory.GetFiles(_directory, BoardKindStore.FileName, SearchOption.AllDirectories).Single();

        File.WriteAllText(path, content);

        var loaded = store.Load(_work);

        Assert.DoesNotContain(1L, loaded.Keys);
        Assert.DoesNotContain(9L, loaded.Keys);
        Assert.Equal(content.Contains("10", StringComparison.Ordinal) ? 1 : 0, loaded.Count);
    }

    [Fact]
    public void GivenARemovedProfile_WhenALateAnswerIsSaved_ThenNoFileIsRecreated()
    {
        var store = new BoardKindStore(_directory);

        store.Save(_work, 1, BoardKind.Scrum);
        store.Delete(_work);
        store.Save(_work, 2, BoardKind.Kanban);

        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
        Assert.Empty(store.Load(_work));
    }

    [Fact]
    public void GivenARemovedThenLoadedProfile_WhenSaving_ThenAnswersAreStoredAgain()
    {
        var store = new BoardKindStore(_directory);

        store.Save(_work, 1, BoardKind.Scrum);
        store.Delete(_work);
        _ = store.Load(_work);
        store.Save(_work, 2, BoardKind.Kanban);

        Assert.Equal(BoardKind.Kanban, store.Load(_work)[2]);
    }

    [Fact]
    public void GivenAStrayTemporaryFile_WhenDeleting_ThenTheFolderIsRemoved()
    {
        var store = new BoardKindStore(_directory);

        store.Save(_work, 1, BoardKind.Scrum);

        var path = Directory.GetFiles(_directory, BoardKindStore.FileName, SearchOption.AllDirectories).Single();

        File.WriteAllText($"{path}.tmp", "half");
        store.Delete(_work);

        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
