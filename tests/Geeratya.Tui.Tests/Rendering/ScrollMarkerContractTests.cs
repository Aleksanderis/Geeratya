using System.Text.RegularExpressions;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of the scroll markers (<c>docs/internal/ui-consistency.md</c>, finding L1; #1034): a
/// list that shows only part of its rows says so in every place it is drawn, with the same marker: <c>▼</c> in the
/// last row when rows are hidden below, <c>▲</c> in the first when rows are hidden above. The frame is built by the real
/// <see cref="FrameBuilder"/> for the list view, the actions menu, the tab list, the command palette and the popup of a
/// field, and the marker is read from the drawn cells. A new place that cuts a list is added to
/// <see cref="Places"/>.
/// </summary>
[Trait("Os", "Independent")]
public class ScrollMarkerContractTests
{
    private const string Below = "▼";
    private const string Above = "▲";
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);

    private readonly AppReducer _reducer = new(_keymap);
    private readonly FieldChoiceContractTests _opener = new();

    /// <summary>The places that cut a list.</summary>
    public static TheoryData<string> Places { get; } = ["list", "actions-menu", "tab-list", "palette", "popup"];

    /// <summary>The places whose list fits in a tall frame (the others are capped in height or in rows).</summary>
    public static TheoryData<string> Fitting { get; } = ["list", "actions-menu"];

    [Theory]
    [MemberData(nameof(Places))]
    public void GivenACutList_WhenTheFirstRowsShow_ThenTheMarkerBelowIsDrawnAndNoneAbove(string place)
    {
        var lines = Frame(Scene(place, false), place);

        Assert.Contains(lines, MarksBelow);
        Assert.DoesNotContain(lines, MarksAbove);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void GivenACutList_WhenTheLastRowsShow_ThenTheMarkerAboveIsDrawnAndNoneBelow(string place)
    {
        var lines = Frame(Scene(place, true), place);

        Assert.Contains(lines, MarksAbove);
        Assert.DoesNotContain(lines, MarksBelow);
    }

    [Theory]
    [MemberData(nameof(Fitting))]
    public void GivenAListThatFits_WhenDrawn_ThenNoMarkerIsDrawn(string place)
    {
        var lines = Frame(Scene(place, false), place, 60);

        Assert.DoesNotContain(lines, MarksBelow);
        Assert.DoesNotContain(lines, MarksAbove);
    }

    [Fact]
    public void GivenTheActionsMenuOnAListRow_WhenDrawn_ThenEachGroupHeadingShowsOnceAndNoCardMoveIsOffered()
    {
        var lines = Frame(Menu(false), "actions-menu", 60);
        var body = string.Join('\n', lines);

        foreach (var heading in new[] { "Issue", "Yank", "Rank" })
        {
            Assert.Equal(1, lines.Count(line => Regex.IsMatch(line, $"│   {heading} +│")));
        }

        Assert.DoesNotContain("previous column", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("palette", "Commands", true)]
    [InlineData("tab-list", "Open tabs", true)]
    [InlineData("jump", "Jump to an issue", false)]
    public void GivenASearchableListOverlay_WhenDrawn_ThenTheInputLineIsFollowedByARuleAndTheCountSitsInTheTopEdge(
        string place,
        string title,
        bool counted)
    {
        var state = place switch
        {
            "palette" => Keys(Rows(3), ":"),
            "tab-list" => Tabs(true),
            _ => _reducer.Reduce(Rows(3), new ActionRequested(ActionIds.IssueJump)).State,
        };
        var lines = Frame(state, place, 40);
        var top = lines.FindIndex(line => line.Contains($"╭─ {title} ", StringComparison.Ordinal));

        Assert.True(top >= 0, string.Join('\n', lines));
        Assert.Matches("│ [>:] ", lines[top + 1][lines[top].IndexOf('╭', StringComparison.Ordinal)..]);
        Assert.Contains('├', lines[top + 2]);
        Assert.Equal(counted, Regex.IsMatch(lines[top], @" \d+/\d+ "));
    }

    private static bool MarksBelow(string line)
    {
        return line.Contains($"{Below}│", StringComparison.Ordinal) || line.TrimEnd().EndsWith(Below, StringComparison.Ordinal);
    }

    private static bool MarksAbove(string line)
    {
        return line.Contains($"{Above}│", StringComparison.Ordinal) || line.TrimEnd().EndsWith(Above, StringComparison.Ordinal);
    }

    private static List<string> Frame(AppState state, string place, int rowsFit = 0)
    {
        var height = rowsFit > 0 ? rowsFit : place == "popup" ? 36 : 18;
        var frame = FrameBuilder.BuildStyled(state, 120, height, _keymap)!;

        return [.. frame.Body.Select(static line => line.PlainText)];
    }

    // The place with a list longer than it shows; <end> scrolls it to its last rows. The "fits" case runs the same
    // scene in a frame tall enough to show everything.
    private AppState Scene(string place, bool end)
    {
        return place switch
        {
            "list" => List(end),
            "actions-menu" => Menu(end),
            "tab-list" => Tabs(end),
            "palette" => Palette(end),
            _ => Popup(end),
        };
    }

    private AppState Rows(int count)
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work) with { Connection = new Connected("Me", "acc-me") },
        };

        state = _reducer.Reduce(state, new ViewportChanged(12, 120, FrameBuilder.BodyHeight(18))).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "GCM");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var rows = Enumerable.Range(1, count)
            .Select(index => new IssueSummary($"{index}", $"GCM-{index}", $"Issue {index}", _status, _type, null, null, [], null, null, "GCM"))
            .ToList();

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, null), _now)).State;
    }

    private AppState List(bool end)
    {
        var state = Rows(40);

        return end ? Keys(state, "G") : state;
    }

    private AppState Menu(bool end)
    {
        var state = Rows(5);
        var menu = ContextMenus.For(state)!;

        return state with { ContextMenu = end ? menu with { Pick = menu.Pick.Moved(menu.Items.Count, menu.Items.Count) } : menu };
    }

    private AppState Tabs(bool end)
    {
        var state = Rows(1);

        for (var index = 2; index <= 14; index++)
        {
            var definition = new ViewDefinition(ViewSourceKind.Jql, $"project = P{index}", ViewLayout.List, $"P{index}");

            state = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        }

        state = _reducer.Reduce(state, new ActionRequested(ActionIds.TabList)).State;

        // The highlight starts on the open tab, the last one.
        return end ? state : Keys(state, [.. Enumerable.Repeat("Up", 13)]);
    }

    private AppState Palette(bool end)
    {
        var state = Keys(Rows(3), ":");

        // More presses than rows: the highlight stops on the last one.
        return end ? Keys(state, [.. Enumerable.Repeat("Down", 200)]) : state;
    }

    private AppState Popup(bool end)
    {
        var field = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, []);
        var state = Keys(_opener.Open("box", field), "a");
        var tab = state.Session.Tabs[0];
        var form = tab.Form!;
        Suggestion[] items = [.. Enumerable.Range(1, 9).Select(static index => new Suggestion($"s{index}", $"Alpha {index}", null, false))];

        state = _reducer.Reduce(state, new SuggestionsLoaded(tab.Id, form.Suggest!.RequestId, items)).State;

        return end ? Keys(state, [.. Enumerable.Repeat("Down", 12)]) : state;
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }
}
