using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.App.Tests;

public sealed class SavedViewEffectHandlerTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-views-{Guid.NewGuid():N}");

    [Fact]
    public void GivenAViewSavedInOneSession_WhenAnotherSessionOpensThePicker_ThenItIsListedAndOpensTheSameView()
    {
        var opened = Run(Start(), "jql project = A").State;
        var saving = new SavedViewEffectHandler(new SavedViewStore(_directory));

        Apply(saving, Run(opened, "view save Mine"));

        var reading = new SavedViewEffectHandler(new SavedViewStore(_directory));
        var picker = Apply(reading, Press(Press(Start(), "Space").State, "t"));
        var chosen = Press(picker, "Enter");

        var tab = Assert.Single(chosen.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "Mine"), tab.Definition);
    }

    [Fact]
    public void GivenAnUnwritableFolder_WhenSaving_ThenTheReplyIsAFailure()
    {
        var blocked = Path.Combine(_directory, "blocked");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(blocked, "a file where the folder should go");

        var handler = new SavedViewEffectHandler(new SavedViewStore(Path.Combine(blocked, "inner")));

        var reply = handler.Run(new SaveViewEffect(_work, new SavedView("A", "Jql", "a = 1", "List")));

        Assert.IsType<SavedViewsFailed>(reply);
    }

    [Fact]
    public void GivenAnotherEffect_WhenRun_ThenTheHandlerIgnoresIt()
    {
        var handler = new SavedViewEffectHandler(new SavedViewStore(_directory));

        Assert.Null(handler.Run(new LoadSourcesEffect(true, true)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = Press(state, ":");

        foreach (var character in command)
        {
            result = Press(result.State, character == ' ' ? "Space" : character.ToString());
        }

        return Press(result.State, "Enter");
    }

    /// <summary>Runs the effects of a result through the handler and feeds every reply back to the reducer.</summary>
    private AppState Apply(SavedViewEffectHandler handler, ReduceResult result)
    {
        var state = result.State;

        foreach (var effect in result.Effects)
        {
            if (handler.Run(effect) is { } reply)
            {
                state = _reducer.Reduce(state, reply).State;
            }
        }

        return state;
    }
}
