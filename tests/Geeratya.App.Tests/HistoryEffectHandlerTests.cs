using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.App.Tests;

public sealed class HistoryEffectHandlerTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-history-{Guid.NewGuid():N}");

    [Fact]
    public void GivenRecordEffects_WhenRun_ThenTheReplyHoldsTheStoredHistoryNewestFirst()
    {
        var handler = new HistoryEffectHandler(new HistoryStore(_directory));

        handler.Run(new RecordHistoryEffect(_work, HistoryKind.Query, "a = 1"));
        handler.Run(new RecordHistoryEffect(_work, HistoryKind.Issue, "gcm-1"));
        var reply = Assert.IsType<HistoryLoaded>(handler.Run(new RecordHistoryEffect(_work, HistoryKind.Query, "b = 2")));

        Assert.Equal(_work, reply.Profile);
        Assert.Equal(["b = 2", "a = 1"], reply.History.Queries);
        Assert.Equal(["GCM-1"], reply.History.Issues);
    }

    [Fact]
    public void GivenStoredHistory_WhenLoadRuns_ThenItIsReturnedForThatProfile()
    {
        var store = new HistoryStore(_directory);

        store.Update(_work, history => history.WithQuery("a = 1"));

        var reply = Assert.IsType<HistoryLoaded>(new HistoryEffectHandler(store).Run(new LoadHistoryEffect(_work)));

        Assert.Equal(["a = 1"], reply.History.Queries);
    }

    [Fact]
    public void GivenStoredHistory_WhenClearRuns_ThenTheFileIsGone()
    {
        var store = new HistoryStore(_directory);

        store.Update(_work, history => history.WithQuery("a = 1"));

        var reply = new HistoryEffectHandler(store).Run(new ClearHistoryEffect(_work));

        Assert.Null(reply);
        Assert.Empty(store.Load(_work).Queries);
    }

    [Fact]
    public void GivenAnotherEffect_WhenRun_ThenTheHandlerLeavesItAlone()
    {
        var handler = new HistoryEffectHandler(new HistoryStore(_directory));

        Assert.Null(handler.Run(new QuitEffect()));
    }

    [Fact]
    public void GivenAnUnwritableStateDirectory_WhenRecording_ThenNoErrorEscapes()
    {
        Directory.CreateDirectory(_directory);

        var blocked = Path.Combine(_directory, "file");

        File.WriteAllText(blocked, "x");

        var handler = new HistoryEffectHandler(new HistoryStore(Path.Combine(blocked, "inner")));

        Assert.Null(handler.Run(new RecordHistoryEffect(_work, HistoryKind.Query, "a = 1")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
