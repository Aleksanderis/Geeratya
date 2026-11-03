using Geeratya.Core.BoardKinds;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.App.Tests;

public sealed class BoardKindSaverTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-saver-{Guid.NewGuid():N}");

    [Fact]
    public void GivenAStore_WhenABoardIsProbed_ThenTheKindIsSavedForTheProfile()
    {
        var store = new BoardKindStore(_directory);

        new BoardKindSaver(_work, store).Probed(7, BoardKind.Kanban);

        Assert.Equal(BoardKind.Kanban, store.Load(_work)[7]);
    }

    [Fact]
    public void GivenAStoreFolderThatIsAFile_WhenABoardIsProbed_ThenTheWriteProblemIsNotReported()
    {
        File.WriteAllText(_directory, "x");

        var saver = new BoardKindSaver(_work, new BoardKindStore(_directory));

        saver.Probed(7, BoardKind.Scrum);
    }

    [Fact]
    public void GivenNoStore_WhenABoardIsProbed_ThenNothingHappens()
    {
        new BoardKindSaver(_work, null).Probed(7, BoardKind.Scrum);
    }

    public void Dispose()
    {
        if (File.Exists(_directory))
        {
            File.Delete(_directory);
        }
        else if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
