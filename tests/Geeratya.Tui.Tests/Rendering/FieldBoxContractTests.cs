using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of a field box (<c>docs/internal/ui-consistency.md</c>, component 3; #1034, findings F1
/// to F3 and P1): the box of a field is the same cells wherever it is drawn. The frame is built by the real
/// <see cref="FrameBuilder"/> for the create form (its Summary and Description boxes), a create sub-task form and the
/// field boxes of the box forms (the transition and the assign forms, which the sprint, link and edit forms share), the
/// box is cut out of the frame and compared cell by cell (glyph and text role) with the same box of the other places,
/// focused and not focused. A new place that draws a field box is added to <see cref="Places"/>.
/// </summary>
[Trait("Os", "Independent")]
public class FieldBoxContractTests
{
    private const int Width = 120;
    private const int Height = 36;
    private const string Corner = "╭";
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FieldChoiceContractTests _opener = new();
    private readonly AppReducer _reducer = new(_keymap);

    /// <summary>The places that draw a field box, with the label of the box focused and of one that is not.</summary>
    public static TheoryData<string, string, string> Places { get; } = new()
    {
        { "create", "Create · GCM · Task", "Description" },
        { "subtask", "Create · GCM · Sub", "Description" },
        { "box", "Notes", "Due date" },
        { "transition", "Notes", "Due date" },
    };

    [Theory]
    [MemberData(nameof(Places))]
    public void GivenAFieldBox_WhenFocused_ThenItsFrameIsTheCellsOfEveryOtherPlace(
        string place,
        string focusedLabel,
        string unfocusedLabel)
    {
        Assert.NotEmpty(unfocusedLabel);

        var expected = Look(Scene("create", FocusOf("create")), "Create · GCM · Task");
        var actual = Look(Scene(place, FocusOf(place)), focusedLabel);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void GivenAFieldBox_WhenNotFocused_ThenItsFrameIsTheCellsOfEveryOtherPlace(
        string place,
        string focusedLabel,
        string unfocusedLabel)
    {
        Assert.NotEmpty(focusedLabel);

        var expected = Look(Scene("create", FocusOf("create")), "Description");
        var actual = Look(Scene(place, FocusOf(place)), unfocusedLabel);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void GivenARequiredField_WhenDrawn_ThenTheOneMarkIsAfterItsLabelAndNoStateNoteIsInTheLabel(
        string place,
        string focusedLabel,
        string unfocusedLabel)
    {
        Assert.NotEmpty(unfocusedLabel);

        var state = Scene(place, FocusOf(place));
        var lines = Frame(state).Body.Select(static line => line.PlainText).ToList();
        var body = string.Join('\n', lines);
        var summary = place is "create" or "subtask" ? focusedLabel : "Summary";

        Assert.Contains($"{summary} *", body, StringComparison.Ordinal);
        Assert.DoesNotContain("required", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[editing]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[navigate]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("┌", body, StringComparison.Ordinal);
        Assert.Equal(2, body.Count(static character => character == '*'));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("box")]
    public void GivenAFocusedLongLabel_WhenDrawn_ThenNothingIsAppendedThatCouldCutIt(string place)
    {
        var state = Scene(place, 3);
        var body = string.Join('\n', Frame(state).Body.Select(static line => line.PlainText));

        Assert.Contains("Due date (yyyy-MM-dd)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Due date (yyyy-MM-dd)  [", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("create", false, ColorSupport.TrueColor)]
    [InlineData("create", true, ColorSupport.TrueColor)]
    [InlineData("create", false, ColorSupport.Basic16)]
    [InlineData("create", true, ColorSupport.Basic16)]
    [InlineData("dropdown", true, ColorSupport.TrueColor)]
    [InlineData("dropdown", true, ColorSupport.Basic16)]
    public void GivenAnEmptyPersonValue_WhenResolved_ThenEveryCellOfTheValueRowIsOnTheBlockFill(
        string place,
        bool focused,
        ColorSupport tier)
    {
        var theme = BuiltInThemes.Default;
        var assignee = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, []);
        var state = place == "dropdown" ? _opener.Open("dropdown", assignee) : ValueScene(assignee, focused);
        var (rowText, firstText) = place == "dropdown" ? ("High ▾", "▲") : ("Unassigned ▾", "[--]");
        var line = Frame(state).Body.Single(row => row.PlainText.Contains(rowText, StringComparison.Ordinal));
        var first = line.Spans.ToList().FindIndex(span => span.Text.Contains(firstText, StringComparison.Ordinal));
        var last = line.Spans.ToList().FindIndex(static span => span.Text.Contains('▾'));
        var fill = Palette.Resolve(theme, tier, focused ? PaletteRole.Selected : PaletteRole.ValueBlock);

        Assert.True(first > 0 && last >= first, line.PlainText);

        foreach (var span in line.Spans.Skip(first - 1).Take(last - first + 3))
        {
            var style = Palette.Resolve(theme, tier, PaletteTable.For(span.Role), span.Ground, true);

            Assert.True(
                tier == ColorSupport.Basic16 ? style.Background16 == fill.Background16 : style.Background == fill.Background,
                $"'{span.Text}' ({span.Role}, {span.Ground}) is not on the block fill");
        }
    }

    private AppState ValueScene(FormField assignee, bool focused)
    {
        var summary = FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, false, []);
        var state = _opener.OpenForm("create", [summary, assignee]);

        return focused ? _reducer.Reduce(state, new KeyPressed("Tab", _now)).State : state;
    }

    private static int FocusOf(string place)
    {
        return place is "create" or "subtask" ? 0 : 2;
    }

    private static StyledFrame Frame(AppState state)
    {
        return FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
    }

    private static List<FormField> Fields()
    {
        return
        [
            FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, []),
            FormField.Create(FormFieldIds.Description, "Description", FormFieldKind.Paragraph, false, []),
            FormField.Create("notes", "Notes", FormFieldKind.Text, true, []),
            FormField.Create("duedate", "Due date (yyyy-MM-dd)", FormFieldKind.Text, false, []),
        ];
    }

    // The form of the place with four fields, the one at index <focus> focused (editing).
    private AppState Scene(string place, int focus)
    {
        var state = _opener.OpenForm(place, Fields());

        for (var step = 0; step < 8 && state.Session.Tabs[0].Form!.Focus != focus; step++)
        {
            state = _reducer.Reduce(state, new KeyPressed("Tab", _now)).State;
        }

        Assert.Equal(focus, state.Session.Tabs[0].Form!.Focus);

        return state;
    }

    // The frame structure of the box titled <label>: the corner and rule cells and the chip lead of the top edge,
    // the cells at both ends of a wall row, of the bottom edge and of the top edge, as glyph and role.
    private static string Look(AppState state, string label)
    {
        var rows = Frame(state).Body.Select(Cells).ToList();
        var top = -1;
        var start = -1;

        for (var row = 0; row < rows.Count && top < 0; row++)
        {
            var text = string.Concat(rows[row].Select(static cell => cell.Glyph));

            start = text.IndexOf($"{Corner}─ {label}", StringComparison.Ordinal);
            top = start >= 0 ? row : -1;
        }

        Assert.True(top >= 0, $"no box titled {label}");

        var close = rows[top].FindIndex(start, static cell => cell.Glyph == "╮");
        var bottom = top + 1;

        while (rows[bottom][start].Glyph != "╰")
        {
            bottom++;
        }

        var cells = new List<Cell>();

        cells.AddRange(rows[top].GetRange(start, 3));
        cells.AddRange(rows[top].GetRange(close - 1, 2));
        cells.AddRange(rows[top + 1].GetRange(start, 2));
        cells.AddRange(rows[top + 1].GetRange(close - 1, 2));
        cells.Add(rows[bottom][start]);
        cells.Add(rows[bottom][start + 1]);
        cells.Add(rows[bottom][close]);

        return string.Join(' ', cells.Select(static cell => $"{cell.Glyph}:{cell.Role}"));
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

    private sealed record Cell(string Glyph, TextRole Role);
}
