using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Mouse;

/// <summary>Middle-click close, hover close and the overflow tab list of the tab row, through the real reducer.</summary>
[Trait("Os", "Independent")]
public class TabRowMouseTests
{
    private const int HeaderRows = 2;

    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenThreeTabs_WhenMiddleClickingAnInactiveChip_ThenThatTabClosesAndTheActiveStays()
    {
        var state = WithTabs(3, 100);
        var active = state.Session.Active;
        var second = TabBar.Areas(state.Session.Tabs, active, 100)[1];

        var result = Resolve(state, 100, second.Start + 2, 0, MouseGesture.MiddleClick);

        Assert.Equal(2, result.Session.Tabs.Count);
        Assert.DoesNotContain(result.Session.Tabs, tab => tab.Id == second.Tab);
        Assert.Equal(active, result.Session.Active);
    }

    [Fact]
    public void GivenThreeTabs_WhenMiddleClickingTheActiveChip_ThenItCloses()
    {
        var state = WithTabs(3, 100);
        var area = TabBar.Areas(state.Session.Tabs, state.Session.Active, 100)[2];

        var result = Resolve(state, 100, area.Start, 0, MouseGesture.MiddleClick);

        Assert.DoesNotContain(result.Session.Tabs, tab => tab.Id == area.Tab);
    }

    [Fact]
    public void GivenThreeTabs_WhenMiddleClickingASeparatorOrEmptyBar_ThenNothingCloses()
    {
        var state = WithTabs(3, 100);
        var areas = TabBar.Areas(state.Session.Tabs, state.Session.Active, 100);

        var onSeparator = Resolve(state, 100, areas[0].End, 0, MouseGesture.MiddleClick);
        var onEmpty = Resolve(state, 100, 60, 0, MouseGesture.MiddleClick);

        Assert.Equal(3, onSeparator.Session.Tabs.Count);
        Assert.Equal(3, onEmpty.Session.Tabs.Count);
    }

    [Fact]
    public void GivenScrolledBar_WhenMiddleClickingAHiddenMark_ThenNothingCloses()
    {
        var state = WithTabs(12, 50);
        var middle = _reducer.Reduce(state, new ActionRequested(ActionIds.TabGoTo6)).State;
        var mark = TabBar.Areas(middle.Session.Tabs, middle.Session.Active, 50).First(area => area.Overflow);

        var result = Resolve(middle, 50, mark.Start, 0, MouseGesture.MiddleClick);

        Assert.Equal(12, result.Session.Tabs.Count);
    }

    [Fact]
    public void GivenAPointerOverAnInactiveChip_WhenHovering_ThenTheChipIsHoveredAndShowsItsX()
    {
        var state = WithTabs(3, 100);
        var first = TabBar.Areas(state.Session.Tabs, state.Session.Active, 100)[0];

        var hovered = Hover(state, 100, first.Start + 2, 0);
        var areas = TabBar.Areas(hovered.Session.Tabs, hovered.Session.Active, 100, null, hovered.HoveredTab);

        Assert.Equal(first.Tab, hovered.HoveredTab);
        Assert.NotNull(areas[0].Close);
        Assert.NotNull(areas[2].Close);
        Assert.Null(areas[1].Close);
        Assert.Contains(" x ", TabBar.Build(hovered.Session.Tabs, hovered.Session.Active, 100, null, hovered.HoveredTab).PlainText);
    }

    [Fact]
    public void GivenAHoveredChip_WhenThePointerLeavesTheTabRow_ThenTheHoverClears()
    {
        var state = WithTabs(3, 100);
        var first = TabBar.Areas(state.Session.Tabs, state.Session.Active, 100)[0];
        var hovered = Hover(state, 100, first.Start + 2, 0);

        var left = Hover(hovered, 100, 10, 8);

        Assert.Null(left.HoveredTab);
    }

    [Fact]
    public void GivenNarrowBar_WhenHovering_ThenNoExtraXAppears()
    {
        var state = WithTabs(3, 70);
        var first = TabBar.Areas(state.Session.Tabs, state.Session.Active, 70)[0];

        var hovered = Hover(state, 70, first.Start + 2, 0);
        var areas = TabBar.Areas(hovered.Session.Tabs, hovered.Session.Active, 70, null, hovered.HoveredTab);

        Assert.All(areas, area => Assert.Null(area.Close));
    }

    [Fact]
    public void GivenAnOverlayIsOpen_WhenHovering_ThenNoChipIsHovered()
    {
        var state = WithTabs(3, 100);
        var opened = _reducer.Reduce(state, new ActionRequested(ActionIds.TabNew)).State;
        var first = TabBar.Areas(opened.Session.Tabs, opened.Session.Active, 100)[0];

        var hovered = Hover(opened, 100, first.Start + 2, 0);

        Assert.Null(hovered.HoveredTab);
    }

    [Fact]
    public void GivenAHoveredInactiveChip_WhenClickingItsX_ThenThatTabClosesNotTheActiveOne()
    {
        var state = WithTabs(3, 100);
        var active = state.Session.Active;
        var first = TabBar.Areas(state.Session.Tabs, active, 100)[0];
        var hovered = Hover(state, 100, first.Start + 2, 0);
        var close = TabBar.Areas(hovered.Session.Tabs, active, 100, null, hovered.HoveredTab)[0].Close!.Value;

        var result = Resolve(hovered, 100, close.Start, 0, MouseGesture.Click);

        Assert.Equal(2, result.Session.Tabs.Count);
        Assert.DoesNotContain(result.Session.Tabs, tab => tab.Id == first.Tab);
        Assert.Equal(active, result.Session.Active);
    }

    [Fact]
    public void GivenAnUnhoveredInactiveChip_WhenClickingWhereTheXWouldBe_ThenItOnlySwitchesTabs()
    {
        var state = WithTabs(3, 100);
        var first = TabBar.Areas(state.Session.Tabs, state.Session.Active, 100)[0];

        var result = Resolve(state, 100, first.End - 2, 0, MouseGesture.Click);

        Assert.Equal(3, result.Session.Tabs.Count);
        Assert.Equal(first.Tab, result.Session.Active);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenScrolledBar_WhenClickingAHiddenMark_ThenTheTabListOpensOnTheActiveTab(bool rightMark)
    {
        var state = WithTabs(12, 50);
        var middle = _reducer.Reduce(state, new ActionRequested(ActionIds.TabGoTo6)).State;
        var areas = TabBar.Areas(middle.Session.Tabs, middle.Session.Active, 50);
        var mark = rightMark ? areas[^1] : areas[0];

        var result = Resolve(middle, 50, mark.Start, 0, MouseGesture.Click);

        Assert.True(mark.Overflow);
        Assert.Equal(PickerScope.Tabs, result.Picker!.Scope);
        Assert.Equal(12, SourceChoices.For(result.Picker, result.Session.Sources).Count);
        Assert.Equal(5, result.Picker.Selected);
        Assert.Equal(middle.Session.Active, result.Session.Active);
    }

    [Fact]
    public void GivenTheTabList_WhenClickingTheFirstRow_ThenThatTabBecomesActive()
    {
        var state = WithTabs(12, 50);
        var opened = _reducer.Reduce(state, new ActionRequested(ActionIds.TabList)).State;
        var target = state.Session.Tabs[0].Id;

        var chosen = Picker(opened, 0);

        Assert.Equal(PickerScope.Tabs, opened.Picker!.Scope);
        Assert.Equal(target, chosen.Session.Active);
        Assert.Null(chosen.Picker);
        Assert.Equal(InputMode.Normal, chosen.Mode);
    }

    [Fact]
    public void GivenTheTabList_WhenTyping_ThenTheRowsAreNarrowedByText()
    {
        var state = WithTabs(12, 50);
        var opened = _reducer.Reduce(state, new ActionRequested(ActionIds.TabList)).State;
        var typed = SourceReducer.Type(opened, "P11");

        var rows = SourceChoices.For(typed.Picker!, typed.Session.Sources);

        Assert.Equal("11 JQL P11", Assert.Single(rows).Name);
    }

    [Fact]
    public void GivenNoTabs_WhenOpeningTheList_ThenItSaysSoAndOpensNothing()
    {
        var empty = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };

        var result = _reducer.Reduce(empty, new ActionRequested(ActionIds.TabList)).State;

        Assert.Null(result.Picker);
        Assert.Equal("No tabs open.", result.Message);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingUpTheTabList_ThenItIsBoundToSpaceT()
    {
        Assert.Equal("<space>T", DefaultKeymap.Load().FindKeyLabel(InputMode.Normal, ActionIds.TabList));
    }

    private AppState Picker(AppState opened, int row)
    {
        var clicked = new OverlayRowClicked(MouseTarget.PickerRow, row, ActionIds.FormNext, _now);

        return _reducer.Reduce(opened, clicked).State;
    }

    private AppState Resolve(AppState state, int width, int column, int row, MouseGesture gesture)
    {
        var context = MouseContext.From(state, width, 24, HeaderRows, _now);
        var current = state;

        foreach (var command in MouseResolver.Resolve(context, column, row, gesture, _now))
        {
            current = _reducer.Reduce(current, command).State;
        }

        return current;
    }

    private AppState Hover(AppState state, int width, int column, int row)
    {
        var context = MouseContext.From(state, width, 24, HeaderRows, _now);

        return _reducer.Reduce(state, MouseResolver.Hover(context, column, row)).State;
    }

    private AppState WithTabs(int count, int width)
    {
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
        state = _reducer.Reduce(state, new ViewportChanged(20, width)).State;

        for (var index = 1; index <= count; index++)
        {
            var definition = new ViewDefinition(ViewSourceKind.Jql, $"project = P{index}", ViewLayout.List, $"P{index}");

            state = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        }

        return state;
    }
}
