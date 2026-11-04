using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The Visual range look in every colour tier (<c>docs/ux-look.md</c>, section 4): stronger than the selected row,
/// the cursor row underlined, rows keeping their status and type colours over the tint.
/// </summary>
[Trait("Os", "Independent")]
public class VisualRangeLookTests
{
    private const double TextContrast = 4.5;
    private const double TintContrast = 3.0;

    private static readonly TextRole[] _rowRoles =
    [
        TextRole.Body,
        TextRole.Strong,
        TextRole.Muted,
        TextRole.StatusToDo,
        TextRole.StatusInProgress,
        TextRole.StatusDone,
        TextRole.TypeEpic,
        TextRole.TypeStory,
        TextRole.TypeBug,
        TextRole.TypeTask,
        TextRole.TypeSubtask,
        TextRole.PriorityHigh,
        TextRole.PriorityMedium,
        TextRole.PriorityLow,
    ];

    public static TheoryData<string> Themes => [.. BuiltInThemes.Names];

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingTheRangeAtTrueColour_ThenItIsADifferentTintFromTheSelectionWithReadableText(
        string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var range = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Range);
        var selected = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Selected);

        Assert.NotEqual(selected.Background, range.Background);
        Assert.True(Contrast.Ratio(range.Foreground!.Value, range.Background!.Value) >= TextContrast, name);
        Assert.True(range.Style.HasFlag(RoleStyle.Bold));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingTheRangeAtSixteenColours_ThenItIsReadableAndNotTheSelectionBlue(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var range = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Range);
        var selected = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Selected);

        Assert.NotEqual(selected.Background16, range.Background16);
        Assert.True(Contrast.Ratio(range.Foreground!.Value, range.Background!.Value) >= TextContrast, name);
        Assert.True(range.Style.HasFlag(RoleStyle.Bold));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingRowSpansOnATint_ThenEveryRoleStaysReadableInEveryTier(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            foreach (var ground in new[] { SpanGround.Selection, SpanGround.Range })
            {
                foreach (var role in _rowRoles)
                {
                    var style = Palette.Resolve(theme, tier, PaletteTable.For(role), ground, true);
                    var ratio = Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);

                    Assert.True(ratio >= TintContrast, $"{name} {tier} {ground} {role}: {ratio:0.0}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenATypeSpanSitsOnTheSelection_ThenItKeepsItsOwnColourWhereReadable(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var own = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.TypeBug);
        var onTint = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.TypeBug, SpanGround.Selection, true);
        var plain = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, SpanGround.Selection, true);
        var kept = Contrast.Ratio(own.Foreground!.Value, onTint.Background!.Value) >= TintContrast;

        Assert.Equal(kept ? own.Foreground : plain.Foreground, onTint.Foreground);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenTheCursorRowOfARangeIsResolved_ThenOnlyItIsUnderlinedInEveryTier(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var ground = SpanGround.Range | SpanGround.Cursor;

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16, ColorSupport.None })
        {
            var cursor = Palette.Resolve(theme, tier, PaletteRole.Body, ground, true);
            var other = Palette.Resolve(theme, tier, PaletteRole.Body, SpanGround.Range, true);

            Assert.True(cursor.Style.HasFlag(RoleStyle.Underline), $"{name} {tier}");
            Assert.False(other.Style.HasFlag(RoleStyle.Underline), $"{name} {tier}");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingWithoutColour_ThenTheRangeIsReversedAndBold(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var style = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Body, SpanGround.Range, true).Style;

        Assert.True(style.HasFlag(RoleStyle.Reverse));
        Assert.True(style.HasFlag(RoleStyle.Bold));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenARowBehindAnOverlay_WhenResolvingTheTint_ThenItsTextIsDimmedLikeTheRestOfTheView(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var ground = SpanGround.Range | SpanGround.Dim;

        var lit = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, SpanGround.Range, true);
        var dimmed = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, ground, true);

        Assert.Equal(lit.Background, dimmed.Background);
        Assert.NotEqual(lit.Foreground, dimmed.Foreground);
    }

    [Fact]
    public void GivenTheRangeTextRole_WhenMappingIt_ThenItUsesTheRangePaletteRole()
    {
        Assert.Equal(PaletteRole.Range, PaletteTable.For(TextRole.Range));
    }
}
