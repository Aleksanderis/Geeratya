using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Focus and selection in every colour tier (<c>docs/ux-look.md</c>, sections 4 and 6): never colour alone, and the
/// new roles keep their contrast in every built-in theme.
/// </summary>
[Trait("Os", "Independent")]
public class FocusColourTests
{
    private const double FrameContrast = 3.0;
    private const double TextContrast = 4.5;

    public static TheoryData<string> Themes => [.. BuiltInThemes.Names];

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingTheFocusRoles_ThenTheyKeepTheirContrastAtTrueColourAndSixteenColours(
        string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            Assert.True(Ratio(theme, tier, PaletteRole.FrameFocus) >= FrameContrast, $"{name} {tier}: frame");
            Assert.True(Ratio(theme, tier, PaletteRole.ChipFocus) >= TextContrast, $"{name} {tier}: chip");
            Assert.True(Ratio(theme, tier, PaletteRole.Divider) >= FrameContrast, $"{name} {tier}: quiet border");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingWithoutColour_ThenFocusAndSelectionStandApartByAttributesAlone(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var frame = Palette.Resolve(theme, ColorSupport.None, PaletteRole.FrameFocus).Style;
        var border = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Divider).Style;
        var chip = Palette.Resolve(theme, ColorSupport.None, PaletteRole.ChipFocus).Style;
        var selected = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Selected).Style;

        Assert.True(frame.HasFlag(RoleStyle.Bold));
        Assert.Equal(RoleStyle.None, border);
        Assert.True(chip.HasFlag(RoleStyle.Reverse) && chip.HasFlag(RoleStyle.Bold));
        Assert.True(selected.HasFlag(RoleStyle.Reverse));
    }

    [Fact]
    public void GivenTheTextRoles_WhenMappingThem_ThenBordersUseTheDividerAndFocusUsesItsOwnRoles()
    {
        Assert.Equal(PaletteRole.Divider, PaletteTable.For(TextRole.Border));
        Assert.Equal(PaletteRole.FrameFocus, PaletteTable.For(TextRole.FrameFocus));
        Assert.Equal(PaletteRole.ChipFocus, PaletteTable.For(TextRole.ChipFocus));
    }

    [Fact]
    public void GivenChipSpans_WhenStylingThem_ThenFocusedIsOneRoleAndDimKeepsOnlyMeaningColours()
    {
        StyledSpan[] spans =
        [
            new("GCM-5", TextRole.Key),
            new(" · ", TextRole.Heading),
            new("Bug", TextRole.TypeBug),
        ];

        var focused = ChipStyle.Focused(spans);
        var dim = ChipStyle.Dim(spans);

        Assert.All(focused, span => Assert.Equal(TextRole.ChipFocus, span.Role));
        Assert.Equal([TextRole.Strong, TextRole.Muted, TextRole.TypeBug], dim.Select(span => span.Role));
        Assert.Equal(spans.Select(span => span.Text), dim.Select(span => span.Text));
    }

    private static double Ratio(Theme theme, ColorSupport tier, PaletteRole role)
    {
        var style = Palette.Resolve(theme, tier, role);

        return Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);
    }
}
