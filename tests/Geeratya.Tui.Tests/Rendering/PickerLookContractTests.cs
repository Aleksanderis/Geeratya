using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of an open picker (<c>docs/internal/ui-consistency.md</c>, component 3; owner decision
/// 2026-11-18, #1027): the box that opens under a field (title, input line, quick rows, search results, highlight) is
/// the one of the issue view's Details dropdown in every presentation, for every picker field kind. The frame is
/// drawn by the real <see cref="FrameBuilder"/>; the picker region is cut out of it, cell by cell (glyph and text
/// role), and compared with the same field's region in the Details dropdown, whatever the position. The width is a
/// function of the room (at most 40 columns), so the cells are compared up to the narrower box and the
/// closing edge cell is compared on its own. Earlier contracts compared reducer states and looked for strings, which
/// is why the look drifted three times (#1021, #1022, #1024). A new presentation or picker kind is added to
/// <see cref="Presentations"/> or <see cref="Kinds"/>.
/// </summary>
[Trait("Os", "Independent")]
public class PickerLookContractTests
{
    private const string Reference = "dropdown";
    private const int Width = 120;
    private const int Height = 36;
    private const string Corner = "╭";
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    private readonly FieldChoiceContractTests _opener = new();
    private readonly AppReducer _reducer = new(_keymap);

    /// <summary>Presentations that draw an open picker, the Details dropdown (the reference) included.</summary>
    public static string[] Presentations { get; } = ["box", "create", "subtask", "transition", "edit-box"];

    /// <summary>
    /// The picker field kinds: person, parent, priority, labels, components, sprint, custom choice and user.
    /// </summary>
    public static string[] Kinds { get; } =
    [
        "assignee",
        "reporter",
        "parent",
        "priority",
        "labels",
        "components",
        "sprint",
        "custom-choice",
        "custom-user",
    ];

    /// <summary>The states of the picker the look is compared in.</summary>
    public static string[] Scenes { get; } = ["open", "highlighted", "results"];

    public static TheoryData<string, string, string> Cases()
    {
        var cases = new TheoryData<string, string, string>();

        foreach (var kind in Kinds)
        {
            foreach (var presentation in Presentations)
            {
                foreach (var scene in Scenes)
                {
                    cases.Add(kind, presentation, scene);
                }
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void GivenAnOpenPicker_WhenDrawn_ThenItsRegionIsTheOneOfTheIssueViewDropdown(
        string kind,
        string presentation,
        string scene)
    {
        var field = FieldOf(kind);
        var expected = RegionOf(Scene(Reference, field, scene), field.Label);
        var actual = RegionOf(Scene(presentation, field, scene), field.Label);

        Assert.True(expected.Count > 2, $"no picker box in the {Reference} for {kind}");
        Assert.True(actual.Count > 2, $"no picker box in the {presentation} for {kind}");
        Assert.Equal(expected.Count, actual.Count);

        var narrower = Math.Min(expected[0].Count, actual[0].Count);

        for (var row = 0; row < expected.Count; row++)
        {
            Assert.Equal(Text(expected[row], narrower - 1), Text(actual[row], narrower - 1));
            Assert.Equal(Roles(expected[row], narrower - 1), Roles(actual[row], narrower - 1));
            Assert.Equal(expected[row][^1].Glyph, actual[row][^1].Glyph);
            Assert.Equal(expected[row][^1].Role, actual[row][^1].Role);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void GivenAnOpenPicker_WhenDrawn_ThenTheCaretIsOnlyInTheBoxInputLine(
        string kind,
        string presentation,
        string scene)
    {
        var field = FieldOf(kind);
        var frame = Frame(Scene(presentation, field, scene));
        var box = RegionOf(frame, field.Label);
        var carets = frame.Body
            .Select(static line => line.Spans.Count(static span => span.Role == TextRole.EditCaret))
            .ToList();

        Assert.Equal(1, carets.Sum());
        Assert.Contains(box[1], cell => cell.Role == TextRole.EditCaret);
    }

    [Fact]
    public void GivenTheCreateSubTaskForm_WhenAnAssigneePickerOpens_ThenTheFieldRowKeepsItsValueAndABoxOpensUnderIt()
    {
        var field = FieldOf("assignee");
        var frame = Frame(Scene("subtask", field, "open"));
        var lines = frame.Body.Select(static line => line.PlainText).ToList();
        var fieldRow = lines.FindIndex(static line => line.Contains("│ Assignee ", StringComparison.Ordinal));
        var boxTop = lines.FindIndex(line => line.Contains($"{Corner}─ Assignee", StringComparison.Ordinal));

        Assert.True(fieldRow >= 0 && boxTop > fieldRow, string.Join("\n", lines));
        Assert.DoesNotContain(lines, static line => line.Contains("[editing]", StringComparison.Ordinal));
        Assert.Contains("acc-eve", lines[fieldRow + 1], StringComparison.Ordinal);
    }

    private static FormField FieldOf(string kind)
    {
        FieldOption[] options = [new("a", "Alpha"), new("b", "Beta"), new("c", "Gamma")];

        return kind switch
        {
            "assignee" => FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, [])
                .WithInitial(FormValue.OfChosen(["acc-eve"])),
            "reporter" => FormField.Create(FormFieldIds.Reporter, "Reporter", FormFieldKind.User, true, [])
                .WithInitial(FormValue.OfChosen(["acc-eve"])),
            "parent" => FormField.Create(FormFieldIds.Parent, "Parent", FormFieldKind.Parent, false, []),
            "labels" => FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, [])
                .WithInitial(FormValue.OfChosen(["backend", "frontend"])),
            "components" => FormField.Create("components", "Components", FormFieldKind.MultiSelect, false, options),
            "sprint" => FormField.Create("sprint", "Sprint", FormFieldKind.Select, false, options),
            "custom-choice" => FormField.Create("customfield_10100", "Flavour", FormFieldKind.Select, false, options),
            "custom-user" => FormField.Create("customfield_10200", "Approver", FormFieldKind.User, false, [])
                .WithInitial(FormValue.OfChosen(["acc-eve"])),
            _ => FormField.Create(FormFieldIds.Priority, "Priority", FormFieldKind.Select, true, options),
        };
    }

    // The picker open in the presentation: just opened, with a row highlighted, or with search results listed.
    private AppState Scene(string presentation, FormField field, string scene)
    {
        var state = _opener.Open(presentation, field);

        if (scene == "open")
        {
            return state;
        }

        if (scene == "results")
        {
            state = Keys(state, "a");

            var form = state.Session.Tabs[0].Form!;
            Suggestion[] items = [new("s1", "Alpha One", null, false), new("s2", "Alpha Two", null, false)];

            if (form.Suggest is { } popup && popup.Items.Count < items.Length)
            {
                var loaded = new SuggestionsLoaded(state.Session.Tabs[0].Id, popup.RequestId, items);

                state = _reducer.Reduce(state, loaded).State;
            }
        }

        return Keys(state, "Down");
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }

    private static StyledFrame Frame(AppState state)
    {
        return FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
    }

    private static List<List<Cell>> RegionOf(AppState state, string label)
    {
        return RegionOf(Frame(state), label);
    }

    // The picker box: from its top edge (a rounded corner, the title chip) down to its bottom edge, as cells.
    private static List<List<Cell>> RegionOf(StyledFrame frame, string label)
    {
        var rows = frame.Body.Select(Cells).ToList();
        var region = new List<List<Cell>>();
        var start = -1;
        var top = -1;

        // The field's own box has the same label and corner: the picker box is the last one, under it.
        for (var row = rows.Count - 1; row >= 0 && top < 0; row--)
        {
            var text = string.Concat(rows[row].Select(static cell => cell.Glyph));

            start = text.IndexOf($"{Corner}─ {label} ", StringComparison.Ordinal);
            top = start >= 0 ? row : -1;
        }

        if (top < 0)
        {
            return region;
        }

        var close = rows[top].FindIndex(start, static cell => cell.Glyph == "╮");

        for (var row = top; row < rows.Count; row++)
        {
            region.Add(rows[row].GetRange(start, Math.Min(close + 1, rows[row].Count) - start));

            if (rows[row][start].Glyph == "╰")
            {
                break;
            }
        }

        return region;
    }

    private static List<Cell> Cells(StyledLine line)
    {
        var cells = new List<Cell>();

        foreach (var span in line.Spans)
        {
            foreach (var rune in span.Text.EnumerateRunes())
            {
                cells.Add(new Cell(rune.ToString(), span.Role));

                if (DisplayWidth.Of(rune) == 2)
                {
                    cells.Add(new Cell(string.Empty, span.Role));
                }
            }
        }

        return cells;
    }

    private static string Text(List<Cell> row, int count)
    {
        return string.Concat(row.Take(count).Select(static cell => cell.Glyph));
    }

    private static string Roles(List<Cell> row, int count)
    {
        return string.Join(',', row.Take(count).Select(static cell => cell.Role));
    }

    private sealed record Cell(string Glyph, TextRole Role);
}
