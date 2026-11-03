using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The tab list (the source picker in its tabs scope) at the snapshot sizes.</summary>
public class TabListOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenOpenTabs_WhenOpeningTheList_ThenTheBoxNamesEveryTabAndTheKeys(int width, int height)
    {
        var lines = FrameBuilder.Build(Opened(3), width, height, _keymap);

        Assert.Contains(lines, line => line.Contains("Open tabs", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("2 JQL P2", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.True(line.Length <= width * 2));
    }

    [Fact]
    public void GivenTheList_WhenTheFilterMatchesNothing_ThenTheBoxSaysSo()
    {
        var state = Opened(3);
        var typed = SourceReducer.Type(state, "zzzz");

        var lines = FrameBuilder.Build(typed, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Nothing to choose from.", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheList_WhenBuildingTheFrame_ThenTheFooterSaysSwitch()
    {
        var lines = FrameBuilder.Build(Opened(8), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("switch", StringComparison.Ordinal));
    }

    private static AppState Opened(int count)
    {
        var reducer = new AppReducer(_keymap);
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };

        for (var index = 1; index <= count; index++)
        {
            var definition = new ViewDefinition(ViewSourceKind.Jql, $"project = P{index}", ViewLayout.List, $"P{index}");

            state = reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        }

        return reducer.Reduce(state, new ActionRequested(ActionIds.TabList)).State;
    }
}
