using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Backlog;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of a value (<c>docs/internal/ui-consistency.md</c>, rendered audit 2026-11-18, #1032):
/// a status, person, priority, estimate or empty value is drawn by <see cref="ValueText"/> in the same spans and roles
/// wherever it appears (list, backlog, board card, Details, relation table, form), the row menu marker never covers
/// a value, and text that is cut says so with the one cut glyph. The frames are drawn by the real
/// <see cref="FrameBuilder"/>; earlier rounds compared reducer states, which is how the look drifted. A new place that
/// shows a value is added to <see cref="Places"/>, not given its own test.
/// </summary>
[Trait("Os", "Independent")]
public class ValueDisplayContractTests
{
    private const string Ada = "Ada Lovelace";
    private const int ChipCells = 2;
    private const string LongTail = "with a summary that is far too long to fit on the first row of a card";
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueStatus _doing = new("3", "In Progress", "indeterminate");
    private static readonly IssueStatus _done = new("5", "Done", "done");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly Sprint _sprint = new(7, "Sprint 1", SprintState.Active, null, null, null, null, 6);

    private static readonly string[] _markerPlaces =
        ["list", "backlog", "board", "board-long", "details", "details-focused-status"];
    private static readonly (int, int)[] _sizes = [(120, 36), (80, 24), (40, 10)];
    private static readonly string[] _markerCells = [".", " "];

    private readonly AppReducer _reducer = new(_keymap);

    /// <summary>Every place a value is drawn.</summary>
    public static string[] Places { get; } = ["list", "backlog", "board", "details", "box"];

    public static TheoryData<string> PlaceData()
    {
        return [.. Places];
    }

    public static TheoryData<string, int, int> MarkerCases()
    {
        var cases = new TheoryData<string, int, int>();

        foreach (var place in _markerPlaces)
        {
            foreach (var size in _sizes)
            {
                cases.Add(place, size.Item1, size.Item2);
            }
        }

        return cases;
    }

    [Theory]
    [InlineData("list")]
    [InlineData("backlog")]
    [InlineData("details")]
    public async Task GivenAStatus_WhenDrawn_ThenItsNameAndDotTakeTheRoleOfItsCategory(string place)
    {
        var spans = Spans(await FrameAsync(place, 120, 36));
        var names = new[] { _todo, _doing, _done }.ToDictionary(status => status.Name);
        var found = new HashSet<string>();

        foreach (var span in spans)
        {
            var text = span.Text.Trim('[', ']', ' ', '●');

            if (names.TryGetValue(text, out var status))
            {
                Assert.Equal(ValueText.StatusRole(status), span.Role);
                found.Add(text);
            }
        }

        // The title status and the relation rows of Details draw the other two statuses; every place draws all three.
        Assert.Equal(names.Keys.Order(), found.Order());
    }

    [Fact]
    public async Task GivenTheSameStatusInEveryPlace_WhenDrawn_ThenTheRoleIsTheSameAcrossThePlaces()
    {
        var roles = new Dictionary<string, HashSet<TextRole>>();
        var names = new[] { _todo.Name, _doing.Name, _done.Name };

        foreach (var place in new[] { "list", "backlog", "details" })
        {
            foreach (var span in Spans(await FrameAsync(place, 120, 36)))
            {
                var text = span.Text.Trim('[', ']', ' ', '●');

                if (names.Contains(text))
                {
                    roles.TryAdd(text, []);
                    roles[text].Add(span.Role);
                }
            }
        }

        Assert.All(roles, pair => Assert.Single(pair.Value));
    }

    [Theory]
    [MemberData(nameof(PlaceData))]
    public async Task GivenAnEmptyPerson_WhenDrawn_ThenTheChipAndTheWordAreTheMutedEmptyValue(string place)
    {
        var spans = Spans(await FrameAsync(place, 120, 36));
        var chip = ValueText.PersonChip(null, ChipCells);
        var word = ValueText.Empty(true);
        var found = 0;

        foreach (var span in spans)
        {
            var text = span.Text.Trim();

            if (text == chip.Text)
            {
                Assert.Equal(chip.Role, span.Role);
                found++;
            }

            if (text == word.Text)
            {
                Assert.Equal(word.Role, span.Role);
                found++;
            }
        }

        Assert.True(found > 0, $"no empty person drawn in {place}");
    }

    [Theory]
    [MemberData(nameof(PlaceData))]
    public async Task GivenAPerson_WhenDrawn_ThenTheChipIsTheOneOfValueTextInThePersonsColour(string place)
    {
        var spans = Spans(await FrameAsync(place, 120, 36));
        var chip = ValueText.PersonChip(Ada, ChipCells);
        var chips = spans.Where(span => span.Text == chip.Text).ToList();

        if (place is "list" or "backlog")
        {
            Assert.DoesNotContain(spans, span => span.Text.Contains(PersonChips.Text("AL", ChipCells)));

            return;
        }

        Assert.NotEmpty(chips);
        Assert.All(chips, span => Assert.Equal(chip.Role, span.Role));
    }

    [Theory]
    [InlineData("board")]
    [InlineData("details")]
    [InlineData("box")]
    public async Task GivenAPriority_WhenDrawn_ThenTheGlyphTakesTheRoleOfItsTier(string place)
    {
        var spans = Spans(await FrameAsync(place, 120, 36));
        var tiers = new Dictionary<string, string> { ["▲"] = "High", ["="] = "Medium", ["▼"] = "Low" };
        var found = 0;

        foreach (var span in spans)
        {
            if (tiers.TryGetValue(span.Text.Trim(), out var tier))
            {
                Assert.Equal(ValueText.PriorityRole(tier), span.Role);
                found++;
            }
        }

        Assert.True(found > 0, $"no priority glyph drawn in {place}");
    }

    [Theory]
    [InlineData("list")]
    [InlineData("backlog")]
    public async Task GivenAnEstimate_WhenDrawn_ThenItIsTheValueTextNumberInTheMutedRole(string place)
    {
        var spans = Spans(await FrameAsync(place, 120, 36));
        var expected = ValueText.Estimate(1.5);
        var span = Assert.Single(spans, candidate => candidate.Text.Contains(expected));

        Assert.Equal(TextRole.Muted, span.Role);
    }

    [Theory]
    [MemberData(nameof(MarkerCases))]
    public async Task GivenTheSelectedRow_WhenTheMarkerIsDrawn_ThenItCoversOnlyBlankCells(
        string place,
        int width,
        int height)
    {
        var state = await StateAsync(place);
        var marked = FrameBuilder.BuildStyled(state, width, height, _keymap)!;

        if (width == 120 && place != "details-focused-status")
        {
            Assert.NotNull(marked.RowMenuMarker);
        }

        var plain = FrameBuilder.BuildStyled(state with { Mode = InputMode.Insert }, width, height, _keymap)!;

        Assert.Equal(plain.Body.Count, marked.Body.Count);

        for (var row = 0; row < marked.Body.Count; row++)
        {
            var drawn = Cells(marked.Body[row]);
            var before = Cells(plain.Body[row]);

            for (var column = 0; column < Math.Min(drawn.Count, before.Count); column++)
            {
                if (drawn[column] != before[column])
                {
                    Assert.Equal(" ", before[column]);
                    Assert.Contains(drawn[column], _markerCells);
                }
            }
        }
    }

    [Theory]
    [InlineData("backlog")]
    [InlineData("details")]
    public async Task GivenTheSelectedRow_WhenTheMarkerIsDrawn_ThenItsValuesAreStillOnTheRow(string place)
    {
        var state = await StateAsync(place);
        var lines = FrameBuilder.BuildStyled(state, 120, 36, _keymap)!.PlainLines();
        var text = string.Join('\n', lines);

        Assert.Contains(place == "backlog" ? " 5" : "moves via transition", text, StringComparison.Ordinal);
        Assert.Contains(_doing.Name, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenALongSummaryInAList_WhenDrawn_ThenTheCutEndsInTheCutGlyph()
    {
        var spans = Spans(await FrameAsync("list", 60, 20));

        Assert.Contains(spans, span => span.Text.Contains("Fix login") && span.Text.TrimEnd().EndsWith('…'));
    }

    [Fact]
    public async Task GivenALongSummaryInAList_WhenDrawnInAscii_ThenTheCutEndsInTheAsciiMapOfTheCutGlyph()
    {
        var state = await StateAsync("list");
        var frame = FrameBuilder.BuildStyled(state, 60, 20, _keymap, null, null, GlyphSet.Ascii)!;
        var row = Assert.Single(frame.Body, line => line.PlainText.Contains("Fix login"));

        Assert.DoesNotContain('…', row.PlainText);
        Assert.Matches(@"[a-z]\.\s", row.PlainText);
    }

    [Fact]
    public void GivenALongSuggestionAndFailedLookup_WhenDrawnAsPopupRows_ThenEveryCutEndsInTheCutGlyph()
    {
        var source = new SuggestionSource(SuggestionKind.Labels, null, []);
        var suggestion = new Suggestion("1", "geeratya-seed-GCM-bug-crash-on-start", null, false);
        var popup = new SuggestionState(
            0,
            source,
            string.Empty,
            [suggestion],
            [suggestion],
            0,
            PickStatus.Failed,
            1,
            "the server answered a long sentence about what went wrong");

        var rows = PickListRenderer.Popup(popup, 20);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.EndsWith("…", row.Line.PlainText.TrimEnd()));
    }

    [Fact]
    public async Task GivenAContextMenuInANarrowTerminal_WhenDrawn_ThenCutRowsEndInTheCutGlyph()
    {
        var state = (await StateAsync("list")) with { Mode = InputMode.Insert };
        var items = new[] { ActionIds.IssueEdit, ActionIds.IssueDelete, ActionIds.Quit }
            .Select(action => new ContextMenuItem(action, "Edit the issue title in place (opens the details form)"))
            .ToList();
        var opened = state with { ContextMenu = new ContextMenuState(items, 0) };

        var frame = FrameBuilder.BuildStyled(opened, 50, 24, _keymap)!;
        var rows = frame.PlainLines().Where(line => line.Contains("Edit the issue")).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains("…", row));
    }

    private static List<string> Cells(StyledLine line)
    {
        var cells = new List<string>();

        foreach (var span in line.Spans)
        {
            foreach (var rune in span.Text.EnumerateRunes())
            {
                cells.Add(rune.ToString());

                for (var extra = 1; extra < TextWidth.Of(rune); extra++)
                {
                    cells.Add(string.Empty);
                }
            }
        }

        return cells;
    }

    private static List<StyledSpan> Spans(StyledFrame frame)
    {
        return [.. frame.Body.SelectMany(static line => line.Spans)];
    }

    private async Task<StyledFrame> FrameAsync(string place, int width, int height)
    {
        return FrameBuilder.BuildStyled(await StateAsync(place), width, height, _keymap)!;
    }

    private async Task<AppState> StateAsync(string place)
    {
        return place switch
        {
            "list" => Listed(ViewLayout.List, ViewSourceKind.Jql),
            "backlog" => Backlog(),
            "board" => await BoardAsync(false),
            "board-long" => await BoardAsync(true),
            "details" => Keys(Detailed(), "Ctrl-l", "j"),
            "details-focused-status" => Keys(Detailed(), "Ctrl-l"),
            _ => Box(),
        };
    }

    private static List<IssueSummary> Rows()
    {
        IssueSprint[] sprints = [new IssueSprint(_sprint.Id, _sprint.Name, _sprint.State)];

        return
        [
            Row("VAL-1", "Fix login redirect after token refresh with a very long summary that never fits", _doing, 1.5)
                with
                {
                    Assignee = new IssueAssignee("a", Ada),
                    Sprints = sprints,
                },
            Row("VAL-2", "Second row", _todo, 5) with { Sprints = sprints },
            Row("VAL-3", "Third row", _done, null) with { Sprints = sprints },
        ];
    }

    private static IssueSummary Row(string key, string summary, IssueStatus status, double? estimate)
    {
        return new IssueSummary("1", key, summary, status, _story, "High", null, [], null, null, "VAL")
        {
            Estimate = estimate,
        };
    }

    private static AppState Listed(ViewLayout layout, ViewSourceKind source)
    {
        var definition = new ViewDefinition(source, "project = VAL", layout, "VAL");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Rows = Rows(),
            Selected = 0,
            SelectedKey = "VAL-1",
            KeyWidth = 5,
            Load = LoadState.Idle,
            RequestId = 0,
        };

        return WithTab(tab);
    }

    private static AppState Backlog()
    {
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Rows = BacklogSections.Order(Rows()),
            Selected = 0,
            SelectedKey = "VAL-1",
            KeyWidth = 5,
            Load = LoadState.Idle,
            RequestId = 0,
            Sprints = [_sprint],
        };

        return WithTab(tab);
    }

    private static AppState WithTab(TabState tab)
    {
        var session = SiteSession.Empty(_profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_profile), Session = session };
    }

    private static async Task<AppState> BoardAsync(bool longSummaries)
    {
        var state = await BoardStateFixtures.CompanyManagedAsync(20);
        var tab = state.Session.Tabs[0];
        var assignee = new IssueAssignee("a", Ada);
        var rows = tab.Rows
            .Select((row, index) => index % 2 == 0 ? row with { Assignee = assignee } : row)
            .Select(row => longSummaries ? row with { Summary = $"{row.Summary} {LongTail}" } : row)
            .ToList();

        var changed = tab with { Rows = rows };

        // The recorded board's first card is the one the cursor draws.
        changed = changed with { SelectedKey = ColumnsNavigation.ColumnsOf(changed)[0].Rows[0].Key };

        return state with { Session = state.Session with { Tabs = [changed] } };
    }

    private AppState Detailed()
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };

        state = _reducer.Reduce(state, new ViewportChanged(
            36 - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows,
            120,
            FrameBuilder.BodyHeight(36))).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = VAL", ViewLayout.List, "VAL");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var page = new IssuePage(Rows(), null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = _reducer.Reduce(listed, new KeyPressed("Enter")).State;
        var detail = shown.Session.Tabs[0].Detail[0];
        var loaded = _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now)).State;
        var children = new[]
        {
            Row("VAL-11", "Sub-task one", _todo, null) with { Assignee = new IssueAssignee("a", Ada) },
            Row("VAL-12", "Sub-task two", _done, null),
        };
        var withChildren = loaded.Session.Tabs[0].Detail[0] with { Children = children };

        return loaded with
        {
            Session = loaded.Session with { Tabs = [loaded.Session.Tabs[0] with { Detail = [withChildren] }] },
        };
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "VAL-1",
            "Fix login redirect after token refresh",
            _doing,
            _story,
            "High",
            new IssueAssignee("a", Ada),
            null,
            [],
            _now.AddDays(-9),
            _now.AddHours(-2),
            "VAL",
            null,
            null,
            [new IssueStub("VAL-11", "Sub-task one", _todo, _story), new IssueStub("VAL-12", "Sub-task two", _done, _story)],
            [],
            []);
    }

    private static AppState Box()
    {
        var assignee = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, [new FieldOption("a", Ada)])
            .WithInitial(FormValue.OfChosen(["a"]));
        var reporter = FormField.Create("reporter", "Reporter", FormFieldKind.User, false, []);
        var priority = FormField.Create(
                FormFieldIds.Priority,
                "Priority",
                FormFieldKind.Select,
                false,
                [new FieldOption("1", "High"), new FieldOption("2", "Low")])
            .WithInitial(FormValue.OfChosen(["1"]));
        var target = new CreateFormTarget("A", "A-1", false, new CreateIssueType("10", "Task", string.Empty, 0, false));
        var form = FormState.Opened(target, 3, null) with
        {
            Fields = [assignee, reporter, priority],
            IsLoading = false,
            Navigable = true,
        };

        return FormOverlaySnapshotTests.WithForm(form);
    }
}
