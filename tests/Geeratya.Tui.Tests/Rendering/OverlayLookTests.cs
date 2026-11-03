using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The floating overlay look (<c>docs/ux-look.md</c>, section 7): every overlay is one <see cref="OverlayBox"/> over a
/// still-visible, dimmed view, with the rounded accent frame and the title chip, at the three snapshot sizes and in the
/// ASCII glyph set.
/// </summary>
[Trait("Os", "Independent")]
public class OverlayLookTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    private static readonly string[] _names =
    [
        "help", "jump", "form", "form-picker", "editor", "contextmenu", "profiles", "confirm", "palette", "picker",
    ];

    public static TheoryData<string, int, int> Screens
    {
        get
        {
            var data = new TheoryData<string, int, int>();

            foreach (var name in _names)
            {
                data.Add(name, 120, 36);
                data.Add(name, 80, 24);
            }

            return data;
        }
    }

    public static TheoryData<string, int, int> AllSizes
    {
        get
        {
            var data = new TheoryData<string, int, int>();

            foreach (var name in _names)
            {
                data.Add(name, 120, 36);
                data.Add(name, 80, 24);
                data.Add(name, 40, 10);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void GivenAnOverlay_WhenBuildingTheFrame_ThenTheBoxHasTheRoundedAccentFrameAndTheTitleChip(
        string name,
        int width,
        int height)
    {
        var spans = Build(name, width, height).Body.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == ChipOf(name) && span.Text.StartsWith(' '));
        Assert.Contains(spans, span => span.Role == FrameOf(name) && span.Text.StartsWith('╭'));
        Assert.Contains(spans, span => span.Role == FrameOf(name) && span.Text.EndsWith('╯'));
    }

    [Theory]
    [MemberData(nameof(AllSizes))]
    public void GivenAnOverlay_WhenBuildingTheFrame_ThenEveryCellOutsideTheBoxIsTheDimmedView(
        string name,
        int width,
        int height)
    {
        var body = Build(name, width, height).Body;

        if (name == "confirm" && width < 60)
        {
            // Too small for the dialog: nothing is drawn over the view and the status line keeps the question.
            Assert.DoesNotContain(body.SelectMany(line => line.Spans), span => span.Ground != SpanGround.Page);

            return;
        }

        var cells = body.SelectMany(line => line.Spans).Where(span => span.Text.Trim().Length > 0).ToList();

        Assert.NotEmpty(cells);
        Assert.All(cells, span => Assert.True(
            span.Ground.HasFlag(SpanGround.Surface) || span.Ground.HasFlag(SpanGround.Dim),
            $"'{span.Text}' ({span.Role}) is neither on the box surface nor dimmed"));
    }

    [Theory]
    [InlineData("form", 120, 36)]
    [InlineData("form", 80, 24)]
    [InlineData("form-picker", 80, 24)]
    public void GivenAFormOverNonEmptyView_WhenBuildingTheFrame_ThenTheViewRowsShowDimmedAroundTheBox(
        string name,
        int width,
        int height)
    {
        var frame = Build(name, width, height);
        var row = frame.Body.First(line => line.PlainText.Contains("Issue 1", StringComparison.Ordinal));

        Assert.All(row.Spans.Where(span => span.Text.Trim().Length > 0), span => Assert.True(span.Ground.HasFlag(SpanGround.Dim)));
    }

    [Theory]
    [MemberData(nameof(AllSizes))]
    public void GivenAnOverlay_WhenBuildingTheFrame_ThenEveryLineHasTheFullWidthAndTheBoxCoversNoMoreThanTheBody(
        string name,
        int width,
        int height)
    {
        var frame = Build(name, width, height);

        Assert.Equal(FrameBuilder.BodyHeight(height), frame.Body.Count);
        Assert.All(frame.Body, line => Assert.Equal(width, TextWidth.Of(line.PlainText)));
        Assert.All(frame.Regions, region => Assert.True(region.Left + region.Width <= width && region.Top + region.Height <= height));
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void GivenAnOverlay_WhenBuildingTheFrame_ThenTheBoxIsCentredAcrossTheWidth(string name, int width, int height)
    {
        var box = Build(name, width, height).Regions.First(region => region.Target is MouseTarget.OverlayBox or MouseTarget.ConfirmBox);

        Assert.InRange(box.Left - (width - box.Left - box.Width), -1, 1);
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void GivenAnOverlay_WhenClickingOutsideItsBox_ThenTheBackdropCancelsAndAClickInsideThatHitsNoRowDoesNothing(
        string name,
        int width,
        int height)
    {
        var state = StateOf(name);
        var frame = Build(name, width, height);
        var box = frame.Regions.First(region => region.Target is MouseTarget.OverlayBox or MouseTarget.ConfirmBox);
        var context = MouseContext.From(state, width, height, frame.Header.Count, default, frame.Regions);
        var outsideRow = box.Top == frame.Header.Count ? box.Top + box.Height : frame.Header.Count;
        var outside = MouseResolver.Resolve(context, 0, outsideRow, MouseGesture.Click, default);
        var edge = MouseResolver.Resolve(context, box.Left + box.Width / 2, box.Top, MouseGesture.Click, default);

        Assert.Single(outside);
        Assert.Empty(edge);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("jump")]
    [InlineData("form")]
    [InlineData("editor")]
    [InlineData("confirm")]
    [InlineData("palette")]
    [InlineData("picker")]
    public void GivenAnOverlayInTheAsciiGlyphSet_WhenBuildingTheFrame_ThenTheFrameIsPlusAndDashesAndTheViewShows(string name)
    {
        var lines = FrameBuilder.Build(StateOf(name), 80, 24, _keymap, NetworkSample, GlyphSet.Ascii);

        Assert.Contains(lines, line => line.Contains("+-", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.DoesNotContain('╭', line));
    }

    [Fact]
    public void GivenAConfirmThatDeletes_WhenBuildingTheFrame_ThenTheFrameIsTheBadColourAndAnyOtherQuestionIsWarn()
    {
        var destructive = new ConfirmRequest("Delete PROJ-1? Jira cannot undo this.", "issue.delete", Label: "Delete issue", Destructive: true);
        var plain = new ConfirmRequest("Move PROJ-1 to Done?", "write.confirm", Label: "Move issue");
        var notice = new ConfirmRequest("Some text", "x", Title: "Goal");

        Assert.Equal(TextRole.OverlayFrameBad, FrameRoleOf(ConfirmOverlayTests.Open(destructive)));
        Assert.Equal(TextRole.OverlayFrameWarn, FrameRoleOf(ConfirmOverlayTests.Open(plain)));
        Assert.Equal(TextRole.OverlayFrame, FrameRoleOf(ConfirmOverlayTests.Open(notice)));
    }

    [Fact]
    public void GivenAWhichKeyPopup_WhenBuildingTheFrame_ThenTheViewAroundItIsNotDimmed()
    {
        var spans = FrameBuilder.BuildStyled(WhichKeyOverlayTests.Opened("Space"), 120, 36, _keymap)!
            .Body.SelectMany(line => line.Spans).ToList();

        Assert.DoesNotContain(spans, span => span.Ground.HasFlag(SpanGround.Dim));
        Assert.Contains(spans, span => span.Role == TextRole.OverlayChip);
    }

    [Fact]
    public void GivenTwoOverlays_WhenOneOpensOverTheOther_ThenTheLowerBoxIsDimmedAndTheUpperIsNot()
    {
        var state = CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("draft")) with
        {
            Mode = InputMode.Confirm,
            Confirm = new ConfirmRequest("Discard the unsent comment text?", "comment.discard"),
        };

        var spans = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!.Body.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.OverlayChip && span.Ground == (SpanGround.Surface | SpanGround.Dim));
        Assert.Contains(spans, span => span.Role == TextRole.OverlayChipWarn && span.Ground == SpanGround.Surface);
    }

    private static DateTimeOffset NetworkSample => NetworkStateSnapshotTests.Now;

    private static TextRole FrameOf(string name)
    {
        return name == "confirm" ? TextRole.OverlayFrameWarn : TextRole.OverlayFrame;
    }

    private static TextRole ChipOf(string name)
    {
        return name == "confirm" ? TextRole.OverlayChipWarn : TextRole.OverlayChip;
    }

    private static TextRole FrameRoleOf(AppState state)
    {
        var spans = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!.Body.SelectMany(line => line.Spans);

        return spans.First(span => span.Text.StartsWith('╭') && span.Role != TextRole.Muted && span.Role != TextRole.Heading).Role;
    }

    private static StyledFrame Build(string name, int width, int height)
    {
        return FrameBuilder.BuildStyled(StateOf(name), width, height, _keymap, null, NetworkSample)!;
    }

    private static AppState StateOf(string name)
    {
        return name switch
        {
            "palette" => PaletteOverlayTests.Sample(string.Empty),
            "picker" => SourcePickerSnapshotTests.Sample(string.Empty),
            _ => AsciiScreenSnapshotTests.State(name),
        };
    }
}
