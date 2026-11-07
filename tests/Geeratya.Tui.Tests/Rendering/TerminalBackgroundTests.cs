using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;
using Geeratya.Tui.Views;
using Terminal.Gui.Drawing;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The terminal's own background under the page (<c>transparent</c> setting, <c>docs/internal/ux-look.md</c>, section 6): the
/// page roles take the default background, fills and the overlay box stay painted, text colours do not change.
/// </summary>
[Trait("Os", "Independent")]
public class TerminalBackgroundTests
{
    private static readonly ColorSupport[] _tiers = [ColorSupport.TrueColor, ColorSupport.Basic16, ColorSupport.None];

    public static TheoryData<string> Themes => [.. BuiltInThemes.Names];

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenTheOptionOff_WhenResolvingAnyRole_ThenNoCellKeepsTheTerminalBackground(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        foreach (var tier in _tiers)
        {
            Assert.All(
                PaletteTable.Roles,
                role => Assert.False(Palette.Resolve(theme, tier, role).DefaultBackground, $"{name} {tier} {role}"));
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenTheOptionOn_WhenResolvingPageRoles_ThenTheyKeepTheTerminalBackgroundInEveryTier(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!.WithTerminalBackground(true);

        foreach (var tier in _tiers)
        {
            foreach (var role in new[] { PaletteRole.Body, PaletteRole.Muted, PaletteRole.Divider, PaletteRole.Heading })
            {
                Assert.True(Palette.Resolve(theme, tier, role).DefaultBackground, $"{name} {tier} {role}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenTheOptionOn_WhenResolvingFills_ThenTheyStayPainted(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!.WithTerminalBackground(true);

        foreach (var tier in _tiers)
        {
            // The bar is unfilled at 16 colours (#691), so it keeps the terminal there.
            foreach (var role in new[] { PaletteRole.Selected, PaletteRole.Range, PaletteRole.Status })
            {
                if (role == PaletteRole.Status && tier == ColorSupport.Basic16)
                {
                    continue;
                }

                Assert.False(Palette.Resolve(theme, tier, role).DefaultBackground, $"{name} {tier} {role}");
            }
        }

        var chip = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.TabActive);

        Assert.False(chip.DefaultBackground);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenTheOptionOn_WhenResolvingAnyRole_ThenTextColoursAndContrastAreTheThemes(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var kept = theme.WithTerminalBackground(true);

        foreach (var role in PaletteTable.Roles)
        {
            Assert.Equal(theme.Entry(role), kept.Entry(role));
            Assert.Equal(
                Palette.Resolve(theme, ColorSupport.TrueColor, role).Foreground,
                Palette.Resolve(kept, ColorSupport.TrueColor, role).Foreground);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenTheOptionOn_WhenDrawingAnOverlay_ThenTheBoxIsPaintedAndTheViewBehindKeepsTheTerminal(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!.WithTerminalBackground(true);
        var box = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, SpanGround.Surface, true);
        var behind = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, SpanGround.Dim, true);

        Assert.False(box.DefaultBackground);
        Assert.Equal(theme.Surface, box.Background);
        Assert.True(behind.DefaultBackground);
        Assert.NotEqual(box.Foreground, behind.Foreground);
    }

    [Fact]
    public void GivenTheSameChoice_WhenAskingForTheTerminalBackgroundAgain_ThenTheThemeIsReusedAndTheChoiceIsKept()
    {
        var theme = BuiltInThemes.Default;

        Assert.Same(theme, theme.WithTerminalBackground(false));
        Assert.False(theme.KeepTerminalBackground);
        Assert.True(theme.WithTerminalBackground(true).KeepTerminalBackground);
    }

    [Fact]
    public void GivenAStyleThatKeepsTheTerminal_WhenMakingAnAttribute_ThenTheBackgroundIsNone()
    {
        var kept = BuiltInThemes.Default.WithTerminalBackground(true);
        var attribute = AttributeFactory.Create(Palette.Resolve(kept, ColorSupport.TrueColor, PaletteRole.Body));
        var painted = AttributeFactory.Create(Palette.Resolve(BuiltInThemes.Default, ColorSupport.TrueColor, PaletteRole.Body));

        Assert.Equal(Color.None, attribute.Background);
        Assert.NotEqual(Color.None, painted.Background);
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    public void GivenTheOptionOn_WhenWritingAnsi_ThenThePageUsesTheDefaultBackgroundCode(ColorSupport tier)
    {
        var frame = new StyledFrame([], [StyledLine.Of("hello")], string.Empty);
        var plain = AnsiFrameWriter.Write(frame, BuiltInThemes.Default, tier, true);
        var kept = AnsiFrameWriter.Write(frame, BuiltInThemes.Default.WithTerminalBackground(true), tier, true);

        Assert.DoesNotContain(plain, line => line.Contains(";49m", StringComparison.Ordinal));
        Assert.Contains(kept, line => line.Contains(";49m", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheOptionOn_WhenResolvingTheBarAtSixteenColours_ThenItKeepsTheTerminalBackground()
    {
        var kept = BuiltInThemes.Default.WithTerminalBackground(true);

        Assert.True(Palette.Resolve(kept, ColorSupport.Basic16, PaletteRole.Status).DefaultBackground);
        Assert.False(Palette.Resolve(kept, ColorSupport.TrueColor, PaletteRole.Status).DefaultBackground);
    }
}
