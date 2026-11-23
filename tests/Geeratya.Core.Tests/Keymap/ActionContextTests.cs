using Geeratya.Core.Boards;
using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Keymap;

/// <summary>Keys that cannot act in the active tab are left out of help and which-key (#1055).</summary>
[Trait("Os", "Independent")]
public class ActionContextTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(ViewSourceKind.Jql, false)]
    [InlineData(ViewSourceKind.SavedFilter, false)]
    [InlineData(ViewSourceKind.Backlog, false)]
    [InlineData(ViewSourceKind.Board, true)]
    [InlineData(ViewSourceKind.Sprint, true)]
    [InlineData(ViewSourceKind.ScrumBacklog, true)]
    public void GivenASource_WhenAskingForSprints_ThenOnlyScrumSourcesHaveThem(ViewSourceKind source, bool expected)
    {
        var tab = StateOf(source, ViewLayout.List, kanban: false).Session.Tabs[0];

        Assert.Equal(expected, ActionContext.HasSprints(tab));
    }

    [Fact]
    public void GivenAKanbanBoard_WhenAskingForSprints_ThenThereAreNone()
    {
        var tab = StateOf(ViewSourceKind.Board, ViewLayout.Columns, kanban: true).Session.Tabs[0];

        Assert.False(ActionContext.HasSprints(tab));
    }

    [Fact]
    public void GivenAKanbanBoard_WhenBuildingTheWhichKeyOfSpace_ThenThereIsNoSprintGroup()
    {
        var kanban = ActionContext.Of(StateOf(ViewSourceKind.Board, ViewLayout.Columns, kanban: true));
        var scrum = ActionContext.Of(StateOf(ViewSourceKind.ScrumBacklog, ViewLayout.List, kanban: false));

        var kanbanLabels = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space", context: kanban)
            .Select(row => row.Label)
            .ToList();
        var scrumLabels = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space", context: scrum)
            .Select(row => row.Label)
            .ToList();

        Assert.DoesNotContain(kanbanLabels, label => label.StartsWith("+sprint", StringComparison.Ordinal));
        Assert.Contains(scrumLabels, label => label.StartsWith("+sprint", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAKanbanBoard_WhenShowingTheColumnsPage_ThenNoSprintKeyIsListed()
    {
        var state = StateOf(ViewSourceKind.Board, ViewLayout.Columns, kanban: true) with { Mode = InputMode.Help };

        var lines = HelpPages.Lines(state, _keymap);

        Assert.DoesNotContain(lines, line => line.Contains("sprint", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.Contains("Move left"));
    }

    [Fact]
    public void GivenAScrumBacklog_WhenShowingItsPage_ThenTheSprintKeysAreListed()
    {
        var state = StateOf(ViewSourceKind.ScrumBacklog, ViewLayout.List, kanban: false) with { Mode = InputMode.Help };

        var lines = HelpPages.Lines(state, _keymap);

        Assert.Contains(lines, line => line.Contains("Move the selected issues to a sprint"));
    }

    [Fact]
    public void GivenAnyPage_WhenListingActions_ThenEachGroupHasAHeaderAndABlankLineBeforeTheNext()
    {
        var state = StateOf(ViewSourceKind.Jql, ViewLayout.List, kanban: false) with { Mode = InputMode.Help };

        foreach (var page in new[] { 0, 1, 2 })
        {
            var lines = HelpPages.Lines(state with { HelpPage = page }, _keymap).ToList();
            var headers = lines.Where(line => line.Length > 0 && !line.StartsWith(' ')).ToList();

            Assert.NotEmpty(headers);

            foreach (var header in headers.Skip(1))
            {
                Assert.Equal(string.Empty, lines[lines.IndexOf(header) - 1]);
            }
        }
    }

    [Fact]
    public void GivenAllActions_WhenKanban_ThenSprintKeysAreStillListed()
    {
        var state = StateOf(ViewSourceKind.Board, ViewLayout.Columns, kanban: true) with { Mode = InputMode.Help };

        var lines = HelpPages.Lines(state with { HelpPage = 2 }, _keymap);

        Assert.Contains(lines, line => line.Contains("Move the selected issues to a sprint"));
    }

    private AppState StateOf(ViewSourceKind source, ViewLayout layout, bool kanban)
    {
        var reducer = new AppReducer(_keymap);
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };
        var opened = reducer.Reduce(start, new ViewOpenRequested(new ViewDefinition(source, "1", layout, "T"))).State;
        var tab = opened.Session.Tabs[0] with { CutoffSource = kanban ? DoneCutoffSource.Jira : null };

        return opened with { Session = opened.Session with { Tabs = [tab] } };
    }
}
