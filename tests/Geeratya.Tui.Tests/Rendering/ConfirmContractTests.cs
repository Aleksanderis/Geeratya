using System.Text.RegularExpressions;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of every confirm and discard dialog (issue #1031, <c>docs/internal/ui-consistency.md</c>,
/// decision 8): each place is driven through the real reducer or builder, drawn by <see cref="FrameBuilder"/> and the
/// box cut out of the frame, so frame role, wording and answer rows are compared cell for cell. A new place that asks
/// through <see cref="ConfirmRequest"/> is added to <see cref="Discards"/> or <see cref="JiraWrites"/>, not given its
/// own test.
/// </summary>
[Trait("Os", "Independent")]
public partial class ConfirmContractTests
{
    private const string CannotUndo = "Jira cannot undo this.";

    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly AppReducer _reducer = new(_keymap);

    public static TheoryData<string, int, int> Discards()
    {
        return Matrix(["new issue", "edit form", "comment editor", "profile form"]);
    }

    public static TheoryData<string, int, int> JiraWrites()
    {
        return Matrix(
        [
            "delete issue",
            "delete comment",
            "unlink",
            "status",
            "rank across sections",
            "sprint move",
            "complete sprint",
            "start sprint",
        ]);
    }

    public static TheoryData<string, int, int> Everything()
    {
        var all = new TheoryData<string, int, int>();

        foreach (var set in new[] { Discards(), JiraWrites() })
        {
            foreach (var row in set)
            {
                all.Add((string)row[0], (int)row[1], (int)row[2]);
            }
        }

        return all;
    }

    [Theory]
    [MemberData(nameof(Discards))]
    public void GivenADiscardDialog_WhenDrawn_ThenItIsYellowWithOneSentenceAndNoUndoClaim(
        string place,
        int width,
        int height)
    {
        var box = BoxOf(Asked(place), width, height);
        var question = string.Join(' ', box.QuestionRows);

        Assert.Equal(TextRole.OverlayFrameWarn, box.FrameRole);
        Assert.Matches(DiscardWording(), question);
        Assert.Single(box.QuestionRows);
        Assert.Empty(box.WarningRows);
        Assert.DoesNotContain("undo", box.AllText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["y|Key Discard|Strong", "n|Key Cancel|Strong"], box.AnswerRows);
    }

    [Theory]
    [MemberData(nameof(JiraWrites))]
    public void GivenAJiraWriteDialog_WhenDrawn_ThenItIsRedWithTheOneUndoSentenceAndNoOtherClaim(
        string place,
        int width,
        int height)
    {
        var box = BoxOf(Asked(place), width, height);

        Assert.Equal(TextRole.OverlayFrameBad, box.FrameRole);
        Assert.Equal([$"{CannotUndo}|Error"], box.WarningRows);
        Assert.Equal(1, Occurrences(box.AllText, "undo"));
        Assert.DoesNotContain("no undo", box.AllText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("n|Key Cancel|Strong", box.AnswerRows[^1]);
        Assert.Equal(2, box.AnswerRows.Count);
        Assert.StartsWith("y|Key ", box.AnswerRows[0], StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Everything))]
    public void GivenAnyDialog_WhenDrawn_ThenTheFrameIsDrawnOnTheSurfaceInOneRoleAndTheViewBehindIsScrimmed(
        string place,
        int width,
        int height)
    {
        var frame = FrameBuilder.BuildStyled(Asked(place), width, height, _keymap)!;
        var box = BoxOf(Asked(place), width, height);
        var theme = BuiltInThemes.Default;

        Assert.All(box.Cells, span => Assert.True(span.Ground.HasFlag(SpanGround.Surface), place));
        Assert.Single(box.Cells.Select(static span => span.Role).Where(static role => role is TextRole.OverlayFrameWarn or TextRole.OverlayFrameBad).Distinct());

        // Every text cell of the view behind the dialog (also the raised box of a form under it) is on the scrim.
        foreach (var span in Outside(frame, box.Region))
        {
            if (span.Role != TextRole.Body || span.Text.Trim().Length > 0
                || (span.Ground & (SpanGround.Selection | SpanGround.Range)) != 0)
            {
                continue;
            }

            var style = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteTable.For(span.Role), span.Ground, true);

            Assert.Equal(theme.ScrimBackground, style.Background);
        }
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenEveryPlace_WhenDrawn_ThenTheCancelRowIsTheSameCellForCell(int width, int height)
    {
        var rows = Discards().Concat(JiraWrites())
            .Where(row => (int)row[1] == width)
            .Select(row => BoxOf(Asked((string)row[0]), width, height).CancelCells)
            .ToList();

        Assert.Equal(12, rows.Count);
        Assert.All(rows, row => Assert.Equal(rows[0], row));
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenEveryDiscardPlace_WhenDrawn_ThenTheAnswerRowsAreTheSameCellForCell(int width, int height)
    {
        var rows = Discards()
            .Where(row => (int)row[1] == width)
            .Select(row => BoxOf(Asked((string)row[0]), width, height).AnswerCells)
            .ToList();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(rows[0], row));
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenEveryJiraWritePlace_WhenDrawn_ThenTheWarningRowIsTheSameCellForCell(int width, int height)
    {
        var rows = JiraWrites()
            .Where(row => (int)row[1] == width)
            .Select(row => BoxOf(Asked((string)row[0]), width, height).WarningCells)
            .ToList();

        Assert.Equal(8, rows.Count);
        Assert.All(rows, row => Assert.Equal(rows[0], row));
    }

    [GeneratedRegex(@"^Discard .+\? Nothing has been saved\.$")]
    private static partial Regex DiscardWording();

    private static TheoryData<string, int, int> Matrix(string[] places)
    {
        var data = new TheoryData<string, int, int>();

        foreach (var place in places)
        {
            data.Add(place, 120, 36);
            data.Add(place, 80, 24);
        }

        return data;
    }

    private static int Occurrences(string text, string word)
    {
        return Regex.Count(text, word, RegexOptions.IgnoreCase);
    }

    private static IEnumerable<StyledSpan> Outside(StyledFrame frame, OverlayRegion region)
    {
        for (var row = 0; row < frame.Body.Count; row++)
        {
            var line = frame.Body[row];

            if (row < region.Top - frame.Header.Count || row >= region.Top - frame.Header.Count + region.Height)
            {
                foreach (var span in line.Spans)
                {
                    yield return span;
                }

                continue;
            }

            foreach (var span in StyledLines.Slice(line, 0, region.Left).Spans
                .Concat(StyledLines.Slice(line, region.Left + region.Width, line.PlainText.Length).Spans))
            {
                yield return span;
            }
        }
    }

    private static DialogBox BoxOf(AppState state, int width, int height)
    {
        Assert.Equal(InputMode.Confirm, state.Mode);

        var frame = FrameBuilder.BuildStyled(state, width, height, _keymap)!;
        var region = Assert.Single(frame.Regions, static candidate => candidate.Target == MouseTarget.ConfirmBox);
        var rows = new List<StyledLine>();

        for (var row = 0; row < region.Height; row++)
        {
            var line = frame.Body[region.Top - frame.Header.Count + row];

            rows.Add(StyledLines.Slice(line, region.Left, region.Left + region.Width));
        }

        return new DialogBox(region, rows);
    }

    private static string CellsOf(StyledLine row)
    {
        var parts = row.Spans
            .Where(static span => !IsFrame(span.Role) && span.Text.Trim().Length > 0)
            .Select(static span => $"{span.Text.Trim()}|{span.Role}|{span.Ground}");

        return string.Join(' ', parts);
    }

    private static bool IsFrame(TextRole role)
    {
        return role is TextRole.OverlayFrameWarn or TextRole.OverlayFrameBad or TextRole.OverlayFrame;
    }

    // The box cut out of a frame: title row, groups of text rows split by blank rows (question, optional warning),
    // the two answer rows and the bottom row.
    private sealed class DialogBox
    {
        private readonly List<StyledLine> _rows;

        public DialogBox(OverlayRegion region, List<StyledLine> rows)
        {
            Region = region;
            _rows = rows;
        }

        public OverlayRegion Region { get; }

        public IEnumerable<StyledSpan> Cells => _rows.SelectMany(static row => row.Spans);

        public TextRole FrameRole => _rows[^1].Spans[0].Role;

        public string AllText => string.Join('\n', _rows.Select(static row => row.PlainText));

        public List<string> QuestionRows => [.. Groups()[0].Select(static row => WallText(row))];

        public List<string> WarningRows
        {
            get
            {
                var groups = Groups();

                return groups.Count < 2 ? [] : [.. groups[1].Select(static row => $"{WallText(row)}|{RoleOf(row)}")];
            }
        }

        public List<string> AnswerRows => [.. _rows.Skip(_rows.Count - 3).Take(2).Select(static row => AnswerText(row))];

        public string CancelCells => CellsOf(_rows[^2]);

        public string AnswerCells => $"{CellsOf(_rows[^3])} / {CellsOf(_rows[^2])}";

        public string WarningCells => string.Join(" / ", WarningRows);

        private List<List<StyledLine>> Groups()
        {
            var groups = new List<List<StyledLine>> { new() };

            foreach (var row in _rows.Skip(1).Take(_rows.Count - 4))
            {
                if (WallText(row).Length == 0)
                {
                    groups.Add([]);
                    continue;
                }

                groups[^1].Add(row);
            }

            return groups;
        }

        private static string AnswerText(StyledLine row)
        {
            return string.Join(' ', row.Spans
                .Where(static span => !IsFrame(span.Role) && span.Text.Trim().Length > 0)
                .Select(static span => $"{span.Text.Trim()}|{span.Role}"));
        }

        private static string WallText(StyledLine row)
        {
            return string.Concat(row.Spans.Skip(1).SkipLast(1).Select(static span => span.Text)).Trim();
        }

        private static TextRole RoleOf(StyledLine row)
        {
            return row.Spans.First(static span => !IsFrame(span.Role) && span.Text.Trim().Length > 0).Role;
        }
    }
}
