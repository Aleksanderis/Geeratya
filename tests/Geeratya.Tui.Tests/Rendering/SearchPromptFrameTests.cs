using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The search prompt that <c>/</c> opens says what it is and what to type (#1055).</summary>
[Trait("Os", "Independent")]
public class SearchPromptFrameTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenSlashPressed_WhenBuildingTheFrame_ThenTheBoxIsTitledSearchAndHintsWhatToType()
    {
        var reducer = new AppReducer(_keymap);
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };

        var state = reducer.Reduce(start, new KeyPressed("/")).State;
        var frame = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.Contains("Search issues", frame);
        Assert.Contains("Type words, or a JQL query.", frame);
    }

    [Fact]
    public void GivenAJqlTab_WhenOpeningTheJqlPrompt_ThenTheCurrentQueryIsInThePrompt()
    {
        var reducer = new AppReducer(_keymap);
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
        var opened = reducer.Reduce(
            start,
            new ViewOpenRequested(new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "T"))).State;

        var state = reducer.Reduce(opened, new KeyPressed(":")).State;

        foreach (var character in "jql")
        {
            state = reducer.Reduce(state, new KeyPressed(character.ToString())).State;
        }

        state = reducer.Reduce(state, new KeyPressed("Enter")).State;

        var frame = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.Contains("> project = GCM", frame);
    }
}
