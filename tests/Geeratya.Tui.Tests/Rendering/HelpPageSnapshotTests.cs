using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The first help page of each layout at the three snapshot sizes, plus a filtered page and the Custom page. To refresh
/// after an intended change run the tests with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class HelpPageSnapshotTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    public static TheoryData<string, int, int> Layouts
    {
        get
        {
            var data = new TheoryData<string, int, int>();

            foreach (var layout in new[] { "list", "columns", "detail", "backlog" })
            {
                data.Add(layout, 120, 36);
                data.Add(layout, 80, 24);
                data.Add(layout, 40, 10);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void GivenALayout_WhenShowingTheFirstHelpPage_ThenItMatchesTheSnapshot(string layout, int width, int height)
    {
        var lines = FrameBuilder.Build(StateFor(layout), width, height, _keymap);

        AssertSnapshot($"help-page-{layout}-{width}x{height}.txt", lines);
    }

    [Fact]
    public void GivenAKeptFilter_WhenShowingTheAllActionsPage_ThenItMatchesTheSnapshot()
    {
        var state = StateFor("list") with { HelpPage = 2, HelpFilter = "tab" };

        AssertSnapshot("help-page-filter-80x24.txt", FrameBuilder.Build(state, 80, 24, _keymap));
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenShowingAPage_ThenTheFilterLineShowsTheCursor()
    {
        var state = StateFor("list") with { HelpFilter = "mo", HelpFiltering = true };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.StartsWith("/mo_", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAKeymapFile_WhenShowingTheCustomPage_ThenItMatchesTheSnapshot()
    {
        const string json = """
            {
              "bindings": [ { "mode": "normal", "keys": "Ctrl-q", "action": "app.quit" } ],
              "unbind": [ { "mode": "normal", "keys": "q" } ]
            }
            """;
        var result = UserKeymap.Merge(_keymap.Bindings, json);
        var custom = new KeymapTable(result.Bindings);

        custom.Replace(result.Bindings, result.Custom);

        var last = HelpPages.Names(StateFor("list"), custom).Count - 1;
        var lines = FrameBuilder.Build(StateFor("list") with { HelpPage = last }, 80, 24, custom);

        AssertSnapshot("help-page-custom-80x24.txt", lines);
    }

    [Fact]
    public void GivenTheFrame_WhenBuildingTheHelp_ThenEachPageNameIsAClickableRegionInTheRowUnderTheTitle()
    {
        var frame = FrameBuilder.BuildStyled(StateFor("list"), 80, 24, _keymap)!;

        var names = HelpPages.Names(StateFor("list"), _keymap);
        var regions = frame.Regions.Where(region => region.Target == MouseTarget.HelpPage).ToList();

        Assert.Equal(names.Count, regions.Count);
        Assert.All(regions, region => Assert.Equal(FrameBuilder.HeaderRows + 1, region.Top));

        var row = frame.Body[1].PlainText;

        for (var index = 0; index < names.Count; index++)
        {
            Assert.Equal(names[index], row.Substring(regions[index].Left, regions[index].Width));
            Assert.Equal(index, regions[index].Index);
        }
    }

    [Fact]
    public void GivenAClickOnAPageName_WhenResolvingAndReducing_ThenThatPageShows()
    {
        var state = StateFor("list");
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var region = frame.Regions.Single(candidate => candidate.Index == 2);
        var context = MouseContext.From(state, 80, 24, FrameBuilder.HeaderRows, _now, frame.Regions);

        var commands = MouseResolver.Resolve(context, region.Left, region.Top, MouseGesture.Click, _now);
        var shown = new AppReducer(_keymap).Reduce(state, commands.Single()).State;

        Assert.Equal(2, shown.HelpPage);
        Assert.Equal(HelpPages.AllName, HelpPages.Names(shown, _keymap)[shown.HelpPage]);
    }

    [Fact]
    public void GivenANarrowTerminal_WhenBuildingTheHelp_ThenNoRegionLeavesTheScreen()
    {
        var frame = FrameBuilder.BuildStyled(StateFor("list"), 12, 10, _keymap)!;

        Assert.All(frame.Regions, region => Assert.True(region.Left + region.Width <= 12));
    }

    private static void AssertSnapshot(string name, IReadOnlyList<string> lines)
    {
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect(name, actual);

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    private static AppState StateFor(string layout)
    {
        var (source, view) = layout switch
        {
            "columns" => (ViewSourceKind.Board, ViewLayout.Columns),
            "backlog" => (ViewSourceKind.Backlog, ViewLayout.List),
            _ => (ViewSourceKind.Jql, ViewLayout.List),
        };
        var reducer = new AppReducer(_keymap);
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var opened = reducer.Reduce(start, new ViewOpenRequested(new ViewDefinition(source, "1", view, "Sample")));
        var tab = opened.State.Session.Tabs[0];
        var sized = reducer.Reduce(opened.State, new ViewportChanged(20));
        var page = new IssuePage([Issue()], null, null);
        var loaded = reducer.Reduce(sized.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = layout == "detail" ? reducer.Reduce(loaded, new KeyPressed("Enter")).State : loaded;

        return shown with { Mode = InputMode.Help };
    }

    private static IssueSummary Issue()
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
