using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Users;

namespace Geeratya.App.Tests;

public sealed class StartupEffectHandlerTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-start-{Guid.NewGuid():N}");

    [Fact]
    public void GivenAFirstRunWithOneBoard_WhenTheBoardOpensAndTheAppRestarts_ThenTheSameBoardOpens()
    {
        var first = new StartupEffectHandler(new RememberedTabsStore(_directory));
        var state = Start(first, Initial());

        state = Apply(first, state, new ConnectionChecked(_work, User()));
        Assert.Equal(PickerScope.Boards, state.Picker!.Scope);

        state = Apply(first, state, new BoardsLoaded([new BoardSummary(4, "GCM board", BoardType.Kanban, "GCM")]));
        Assert.Equal("GCM board", Assert.Single(state.Session.Tabs).Definition.Title);
        first.Flush();

        var second = new StartupEffectHandler(new RememberedTabsStore(_directory));
        var restarted = Start(second, Initial());

        var tab = Assert.Single(restarted.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "GCM board"), tab.Definition);
        Assert.Null(restarted.Picker);
    }

    [Fact]
    public void GivenAFileThatCannotBeWritten_WhenATabOpens_ThenTheFailureIsReported()
    {
        var blocked = Path.Combine(_directory, "blocked");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(blocked, "a file where the folder should go");

        var handler = new StartupEffectHandler(new RememberedTabsStore(Path.Combine(blocked, "inner")));
        var notices = new List<string>();

        handler.SaveFailed += notices.Add;
        handler.Queue(new SaveTabsEffect(_work, RememberedTabs.Empty));
        handler.Flush();

        Assert.Contains("Could not save", Assert.Single(notices));
    }

    [Fact]
    public void GivenAnotherEffect_WhenRun_ThenTheHandlerIgnoresIt()
    {
        var handler = new StartupEffectHandler(new RememberedTabsStore(_directory));

        Assert.Null(handler.Run(new LoadSourcesEffect(true, true)));
    }

    [Fact]
    public void GivenQueuedSaves_WhenFlushed_ThenTheLatestIsWhatTheFileHolds()
    {
        var store = new RememberedTabsStore(_directory);
        var handler = new StartupEffectHandler(store);

        for (var count = 0; count <= 20; count++)
        {
            var tabs = Enumerable.Range(0, count).Select(index => new RememberedTab("Jql", $"a = {index}", "List", "A"));

            handler.Queue(new SaveTabsEffect(_work, new RememberedTabs([.. tabs])));
        }

        handler.Flush();

        Assert.Equal(20, store.Load(_work).Items.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static AppState Initial()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private AppState Start(StartupEffectHandler handler, AppState state)
    {
        return Apply(handler, state, new StartupRequested());
    }

    private AppState Apply(StartupEffectHandler handler, AppState state, Command command)
    {
        var result = _reducer.Reduce(state, command);

        foreach (var effect in result.Effects)
        {
            if (effect is SaveTabsEffect save)
            {
                handler.Queue(save);
            }
            else if (handler.Run(effect) is { } reply)
            {
                return Apply(handler, result.State, reply);
            }
        }

        return result.State;
    }

    private static JiraUser User()
    {
        return new JiraUser("acc-1", "Test User", null, true, null, null);
    }
}
