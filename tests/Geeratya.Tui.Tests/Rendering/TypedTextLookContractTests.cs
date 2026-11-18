using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of typed text and its caret (<c>docs/internal/ui-consistency.md</c>, "Rendered audit
/// 2026-11-18", T1 and T2; issue #1033). Every text surface is drawn by the real <see cref="FrameBuilder"/> with the
/// same text typed into it and the caret on its fourth character; the five cells of the typed word are cut out of the
/// frame (glyph, text role) and resolved by <see cref="Palette"/> in each colour tier (24-bit, 16-colour, no colour),
/// then compared cell for cell with the first surface: the same glyphs, the same role and the same resolved look on
/// the plain page, whatever the position. The ground of a place (an overlay's surface) is the place's own. The
/// <see cref="CaretText.Marked"/> surfaces (Help title, status bar) are one plain string and outside the matrix. Earlier contracts compared reducer states and role lists, so the same text drew
/// on the selection fill in one place and underlined in another. A new text surface is added to
/// <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class TypedTextLookContractTests
{
    private const string Typed = "hello";
    private const int CaretIndex = 3;
    private const int Width = 120;
    private const int Height = 36;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly string[] _typing = ["h", "e", "l", "l", "o", "Left", "Left"];

    private readonly TextEditorContractTests _editors = new();
    private readonly FormPresentationContractTests _forms = new();
    private readonly FieldChoiceContractTests _choices = new();

    /// <summary>Every place that draws typed text with a caret.</summary>
    public static string[] Surfaces { get; } =
    [
        "palette",
        "picker",
        "jump",
        "profile",
        "comment",
        "box-text",
        "box-paragraph",
        "create-summary",
        "create-description",
        "create-detail-text",
        "dropdown-text",
        "in-place-title",
        "dropdown-assignee",
        "dropdown-labels",
        "create-assignee",
    ];

    public static TheoryData<string> SurfaceCases()
    {
        return [.. Surfaces];
    }

    public static TheoryData<string, ColorSupport> TierCases()
    {
        var cases = new TheoryData<string, ColorSupport>();

        foreach (var surface in Surfaces)
        {
            foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16, ColorSupport.None })
            {
                cases.Add(surface, tier);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(TierCases))]
    public void GivenTypedText_WhenDrawnInATier_ThenItsCellsAreTheOnesOfEverySurface(string surface, ColorSupport tier)
    {
        var expected = Cells(Surfaces[0]);
        var actual = Cells(surface);

        Assert.Equal(Glyphs(expected), Glyphs(actual));
        Assert.Equal(Roles(expected), Roles(actual));
        Assert.Equal(Looks(expected, tier), Looks(actual, tier));
    }

    [Theory]
    [MemberData(nameof(SurfaceCases))]
    public void GivenTypedText_WhenDrawn_ThenItIsTheBodyRoleAndOneCaretCellCoversTheCharacterUnderIt(string surface)
    {
        var cells = Cells(surface);

        Assert.Equal(Typed, Glyphs(cells));
        Assert.All(
            cells.Where(static (_, index) => index != CaretIndex),
            static cell => Assert.Equal(TextRole.Body, cell.Role));
        Assert.Equal(TextRole.EditCaret, cells[CaretIndex].Role);
    }

    [Theory]
    [MemberData(nameof(TierCases))]
    public void GivenTypedText_WhenDrawnInATier_ThenTheTextIsPlainAndTheCaretIsToldApartWithoutRelyingOnColour(
        string surface,
        ColorSupport tier)
    {
        var cells = Cells(surface);
        var text = Resolve(cells[0], tier);
        var caret = Resolve(cells[CaretIndex], tier);

        Assert.Equal(RoleStyle.None, text.Style);
        Assert.True(caret.Style.HasFlag(RoleStyle.Reverse) || tier != ColorSupport.None, "caret needs a mark");
        Assert.NotEqual(text, caret);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("profile")]
    public void GivenAnErrorOnTheFocusedField_WhenDrawn_ThenTheRowUnderTheFieldAndTheStatusLineSayItTheSameWay(
        string form)
    {
        const string message = "Not a valid value";

        var state = form == "create" ? CreateWithError(message) : ProfileWithError(message);
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var row = frame.Body.SelectMany(static line => line.Spans)
            .Single(span => span.Text.Contains(message, StringComparison.Ordinal));

        Assert.Equal(TextRole.Error, row.Role);
        Assert.Contains($"! {message}", row.Text, StringComparison.Ordinal);

        var status = $"! {(form == "create" ? "Estimate" : "Account email")}: {message}";

        Assert.Contains(
            FrameBuilder.Build(state, Width, Height, _keymap),
            line => line.Contains(status, StringComparison.Ordinal));
    }

    private static ResolvedStyle Resolve(Cell cell, ColorSupport tier)
    {
        return Palette.Resolve(BuiltInThemes.Default, tier, PaletteTable.For(cell.Role), cell.Ground, true);
    }

    private static string Glyphs(List<Cell> cells)
    {
        return string.Concat(cells.Select(static cell => cell.Glyph));
    }

    private static string Roles(List<Cell> cells)
    {
        return string.Join(',', cells.Select(static cell => cell.Role));
    }

    // The resolved look of each cell on the plain page: text colour, fill and style; the ground of the place (an
    // overlay's raised surface) is the place's own and is left out so only the role decides.
    private static string Looks(List<Cell> cells, ColorSupport tier)
    {
        return string.Join(
            '|',
            cells.Select(cell =>
            {
                var style = Palette.Resolve(BuiltInThemes.Default, tier, PaletteTable.For(cell.Role));

                return $"{style.Foreground}/{style.Background}/{style.Foreground16}/{style.Background16}/{style.Style}";
            }));
    }

    // The five cells of the typed word with the caret on the fourth, cut out of the frame of the surface.
    private List<Cell> Cells(string surface)
    {
        var frame = FrameBuilder.BuildStyled(Open(surface), Width, Height, _keymap)!;

        foreach (var line in frame.Body)
        {
            var row = RowCells(line);
            var text = Glyphs(row);

            for (var start = text.IndexOf(Typed, StringComparison.Ordinal);
                start >= 0;
                start = text.IndexOf(Typed, start + 1, StringComparison.Ordinal))
            {
                var window = row.GetRange(start, Typed.Length);

                if (window[CaretIndex].Role == TextRole.EditCaret)
                {
                    return window;
                }
            }
        }

        throw new InvalidOperationException($"no typed text with a caret in the frame of {surface}");
    }

    private static List<Cell> RowCells(StyledLine line)
    {
        var cells = new List<Cell>();

        foreach (var span in line.Spans)
        {
            foreach (var rune in span.Text.EnumerateRunes())
            {
                cells.Add(new Cell(rune.ToString(), span.Role, span.Ground));
            }
        }

        return cells;
    }

    private AppState Open(string surface)
    {
        switch (surface)
        {
            case "palette":
            case "picker":
            case "jump":
            case "profile":
            case "comment":
                return _editors.Keys(TextEditorContractTests.Open(surface), _typing);
            case "box-text":
                return _editors.Keys(TextEditorContractTests.Open("form-text"), _typing);
            case "box-paragraph":
                return _editors.Keys(TextEditorContractTests.Open("form-paragraph"), _typing);
            case "create-summary":
                return Create(0);
            case "create-description":
                return Create(1);
            case "create-detail-text":
                return Create(6);
            case "dropdown-text":
                return _editors.Keys(_forms.Open("dropdown", TextField()), _typing);
            case "in-place-title":
                return _editors.Keys(_forms.Open("in-place", TextField()), _typing);
            case "dropdown-assignee":
                return _editors.Keys(_choices.Open("dropdown", Person()), _typing);
            case "dropdown-labels":
                return _editors.Keys(
                    _choices.Open("dropdown", FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, [])),
                    _typing);
            default:
                return _editors.Keys(_choices.Open("create", Person()), _typing);
        }
    }

    // The create form with the focus on one field and the word typed into it.
    private static AppState Create(int focus)
    {
        var form = CreatePaneOverlaySnapshotTests.Form();
        var fields = form.Fields.ToArray();

        fields[focus] = fields[focus].WithInitial(FormValue.OfText(Typed)) with { Back = Typed.Length - CaretIndex };

        return CreatePaneOverlaySnapshotTests.Open(form with { Fields = fields, Focus = focus });
    }

    private static AppState CreateWithError(string message)
    {
        var form = CreatePaneOverlaySnapshotTests.Form();
        var fields = form.Fields.ToArray();

        fields[6] = fields[6] with { Label = "Estimate", Error = message };

        return CreatePaneOverlaySnapshotTests.Open(form with { Fields = fields, Focus = 6 });
    }

    private static AppState ProfileWithError(string message)
    {
        var form = ProfileForm.Start with { Field = ProfileFormField.Email, Error = message };

        return AppState.Initial with { Mode = InputMode.Insert, Form = form };
    }

    private static FormField TextField()
    {
        return FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, []);
    }

    private static FormField Person()
    {
        return FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, []);
    }

    private sealed record Cell(string Glyph, TextRole Role, SpanGround Ground);
}
