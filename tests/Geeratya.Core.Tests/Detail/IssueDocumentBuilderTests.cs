using Geeratya.Core.Adf;
using Geeratya.Core.Detail;
using Geeratya.Core.Tests.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Detail;

[Trait("Os", "Independent")]
public class IssueDocumentBuilderTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IssueStatus _status = new("1", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Story", 0, false);

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    public void GivenRichDescriptionAndWideComments_WhenBuilding_ThenNoLineIsWiderThanTheWidth(int width)
    {
        var description = AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription);
        var comments = new[]
        {
            Comment("日本語の作者", "日本語のコメント。これはとても長い文章で、折り返しが必要になります。"),
            Comment("Ünïcode Wríter", "café and emoji 😀 mixed in a long enough sentence to wrap more than once"),
        };

        var document = IssueDocumentBuilder.Build(Loaded(description, comments), width);

        Assert.Equal(width, document.Width);
        Assert.All(document.Lines, line => Assert.True(DisplayWidth.Of(line.PlainText) <= width, line.PlainText));
        Assert.True(document.Lines.Count > 20);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public void GivenTinyOrInvalidWidth_WhenBuilding_ThenItNeverThrowsAndStillHasContent(int width)
    {
        var description = AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription);

        var document = IssueDocumentBuilder.Build(Loaded(description, [Comment("Eve", "Text")]), width);

        Assert.True(document.Lines.Count > 5);
        Assert.True(document.Width >= 1);
    }

    [Fact]
    public void GivenRichDescription_WhenBuilding_ThenItIsRenderedAsFormattedTextUnderTheHeading()
    {
        var description = AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription);
        var expected = AdfRenderer.Render(description, 76).Lines.Select(line => line.PlainText).Take(12).ToList();

        var lines = Texts(IssueDocumentBuilder.Build(Loaded(description, []), 80));

        var heading = lines.FindIndex(line => line.StartsWith("╭─ Description "));
        Assert.True(heading > 0);
        Assert.Equal(expected.Select(text => text.TrimEnd()), lines.Skip(heading + 1).Take(expected.Count));
    }

    [Fact]
    public void GivenHeader_WhenBuilding_ThenEditableFieldsCarryMarkerAndLabelsHaveOwnRole()
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []), 120);
        var spans = document.Lines.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.FieldLabel && span.Text.StartsWith("Status"));
        Assert.Contains(spans, span => span.Role == TextRole.Editable && span.Text == " ▾");
        Assert.DoesNotContain(
            document.Lines.Where(line => line.PlainText.Contains("Reporter")),
            line => line.Spans.Any(span => span.Role == TextRole.Editable && line.PlainText.EndsWith('▾')));
    }

    [Fact]
    public void GivenWideTerminal_WhenBuilding_ThenFieldsShareLinesAndNarrowTerminalsPutOneOnEach()
    {
        var wide = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 120));
        var narrow = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 50));

        Assert.Contains(wide, line => line.Contains("Status    In Progress") && line.Contains("Priority  High"));
        Assert.Contains("Status    In Progress ▾", narrow);
        Assert.Contains("Priority  High ▾", narrow);
    }

    [Fact]
    public void GivenSprintsEstimateAndEpic_WhenBuilding_ThenDetailShowsCurrentSprintEarlierOnesPointsAndEpic()
    {
        var detail = Loaded(null, []) with
        {
            Issue = Issue(null) with
            {
                Parent = new IssueStub("A-0", "Onboarding", _status, new IssueTypeSummary("11", "Epic", 1, false)),
                Sprints =
                [
                    new IssueSprint(1, "Sprint 1", SprintState.Closed),
                    new IssueSprint(2, "Sprint 2", SprintState.Active),
                ],
                Estimate = 5,
            },
        };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 140));

        Assert.Contains(lines, line => line.Contains("Sprint    Sprint 2 (active); earlier: Sprint 1"));
        Assert.Contains(lines, line => line.Contains("Estimate  5"));
        Assert.Contains(lines, line => line.Contains("Epic      A-0 Onboarding"));
        Assert.DoesNotContain(lines, line => line.Contains("Parent"));
    }

    [Fact]
    public void GivenActiveAndFutureSprints_WhenBuilding_ThenOnlyClosedSprintsAreEarlier()
    {
        var detail = Loaded(null, []) with
        {
            Issue = Issue(null) with
            {
                Sprints =
                [
                    new IssueSprint(2, "Sprint 2", SprintState.Active),
                    new IssueSprint(3, "Sprint 3", SprintState.Future),
                ],
            },
        };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 140));

        Assert.Contains(lines, line => line.Contains("Sprint 2 (active)") && !line.Contains("earlier"));
    }

    [Fact]
    public void GivenNoSprintEstimateOrEpic_WhenBuilding_ThenThoseFieldsAreQuiet()
    {
        var text = string.Join('\n', Texts(IssueDocumentBuilder.Build(Loaded(null, []), 140)));

        Assert.DoesNotContain("Sprint", text);
        Assert.DoesNotContain("Estimate", text);
        Assert.DoesNotContain("Epic", text);
    }

    [Fact]
    public void GivenMissingValues_WhenBuilding_ThenThoseFieldsAreOmitted()
    {
        var detail = Loaded(null, []) with { Issue = Issue(null) with { Priority = null, Reporter = null, Assignee = null, Labels = [] } };

        var text = string.Join('\n', Texts(IssueDocumentBuilder.Build(detail, 120)));

        Assert.DoesNotContain("Priority", text);
        Assert.DoesNotContain("Reporter", text);
        Assert.DoesNotContain("Assignee", text);
        Assert.DoesNotContain("Labels", text);
    }

    [Fact]
    public void GivenVeryLongLabelList_WhenBuilding_ThenTheFieldWrapsInsteadOfBeingCut()
    {
        var labels = Enumerable.Range(1, 40).Select(number => $"label-number-{number}").ToArray();
        var detail = Loaded(null, []) with { Issue = Issue(null) with { Labels = labels } };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 60));

        Assert.Contains(lines, line => line.StartsWith("Labels    label-number-1,"));
        Assert.Contains(lines, line => line.Contains("label-number-40"));
    }

    [Fact]
    public void GivenCommentEditedLongAfterWritingAndOneWithoutBodyOrAuthor_WhenBuilding_ThenTheMarkersShow()
    {
        var edited = Comment("Eve", "Text") with { Updated = _now.AddHours(-1) };
        var empty = new IssueComment("c9", null, AdfDocument.Empty, _now.AddMinutes(-5), _now.AddMinutes(-5));

        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, [edited, empty]), 80));

        Assert.Contains("Eve · 2 h ago (edited)", lines);
        Assert.Contains("Unknown · 5 min ago", lines);
        Assert.Contains("(no text)", lines);
    }

    [Fact]
    public void GivenControlCharactersInNames_WhenBuilding_ThenNoLineCarriesThem()
    {
        var comment = Comment($"Mal{(char)27}[31mory{(char)10}", "Body");
        var detail = Loaded(null, [comment]) with { Issue = Issue(null) with { Summary = $"Sum{(char)7}mary{(char)13}{(char)10}line" } };

        var document = IssueDocumentBuilder.Build(detail, 80);

        Assert.All(document.Lines, line => Assert.DoesNotContain(line.PlainText, char.IsControl));
    }

    [Fact]
    public void GivenLoadingCommentsAndNoIssue_WhenBuilding_ThenLoadingLinesStandWhereTheContentWillBe()
    {
        var detail = DetailState.Opened("A-1", null, 1, 2, 3);

        var lines = Texts(IssueDocumentBuilder.Build(detail, 80));

        Assert.StartsWith("╭─ A-1 ", lines[0]);
        Assert.Equal(3, lines.Count(line => line == "Loading…"));
    }

    [Fact]
    public void GivenDocumentTwiceForTheSameInput_WhenBuilding_ThenTheLinesAreEqual()
    {
        var description = AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription);
        var detail = Loaded(description, [Comment("Eve", "Text")]);

        var first = Texts(IssueDocumentBuilder.Build(detail, 100));
        var second = Texts(IssueDocumentBuilder.Build(detail, 100));

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(3599, "59 min ago")]
    [InlineData(3600, "1 h ago")]
    [InlineData(86_399, "23 h ago")]
    [InlineData(86_400, "1 d ago")]
    [InlineData(2_505_600, "29 d ago")]
    [InlineData(2_592_000, "2026-10-02")]
    public void GivenAge_WhenFormattingRelativeTime_ThenItReadsShort(int seconds, string expected)
    {
        var text = RelativeTime.Format(_now.AddSeconds(-seconds), _now);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void GivenTimeAfterTheReference_WhenFormattingRelativeTime_ThenItShowsTheDate()
    {
        Assert.Equal("2026-11-01", RelativeTime.Format(_now.AddMinutes(1), _now));
    }

    [Fact]
    public void GivenLoadedIssueWithoutRelations_WhenBuilding_ThenHeaderDescriptionAndCommentsEachGetAFramedPanel()
    {
        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, [Comment("Eve", "Text")]), 80));

        var tops = lines.Where(line => line.StartsWith("╭─ ")).Select(line => line.Split(' ')[1]).ToList();
        Assert.Equal(["A-1", "Description", "Comments"], tops);
        Assert.Equal(3, lines.Count(line => line.StartsWith('╰')));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    [InlineData(12)]
    public void GivenAnyFramedWidth_WhenBuilding_ThenEveryPanelLineIsExactlyAsWideAsTheTerminal(int width)
    {
        var description = AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription);
        var comments = new[] { Comment("日本語の作者", "日本語のコメント。これはとても長い文章です。") };

        var document = IssueDocumentBuilder.Build(Loaded(description, comments), width);
        Assert.All(document.Lines, line => Assert.Equal(width, DisplayWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenHeaderPanel_WhenBuilding_ThenTheTitleHoldsKeyAndTypeAndTheSummaryIsInside()
    {
        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 80));

        Assert.StartsWith("╭─ A-1 · Story ", lines[0]);
        Assert.Equal("A summary", lines[1]);
    }

    [Fact]
    public void GivenShortAndLongFields_WhenBuilding_ThenShortOnesPairAndTheParentKeepsItsOwnRow()
    {
        var detail = Loaded(null, []) with
        {
            Issue = Issue(null) with { Parent = new IssueStub("A-0", new string('x', 60), _status, _type) },
        };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 100));

        Assert.Contains(lines, line => line.StartsWith("Status    In Progress") && line.Contains("Priority  High"));
        Assert.Contains(lines, line => line.StartsWith("Parent    A-0 xxxx"));
    }

    [Fact]
    public void GivenFailedIssueLoad_WhenBuilding_ThenTheErrorSitsInItsOwnPanel()
    {
        var detail = DetailState.Opened("A-9", null, 1, 2, 3) with
        {
            IssueLoad = new Failed(JiraErrorKind.NotFound, "x"),
        };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 80));

        Assert.Contains(lines, line => line.StartsWith("╭─ Error "));
        Assert.Contains("Not found or no access: A-9", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("╭─ Description"));
    }

    [Fact]
    public void GivenTinyWidth_WhenBuilding_ThenContentIsDrawnWithoutAFrame()
    {
        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 10));

        Assert.DoesNotContain(lines, line => line.Contains('╭'));
        Assert.Contains("Description", lines);
    }

    private static List<string> Texts(DetailDocument document)
    {
        return [.. document.Lines.Select(static line => Inside(line.PlainText))];
    }

    /// <summary>The text between the side borders; border lines stay as they are.</summary>
    private static string Inside(string text)
    {
        return text.StartsWith("│ ") && text.EndsWith(" │") ? text[2..^2].TrimEnd() : text;
    }

    private static DetailState Loaded(AdfDocument? description, IReadOnlyList<IssueComment> comments)
    {
        return DetailState.Opened("A-1", null, 0, 0, 0) with
        {
            Issue = Issue(description),
            IssueLoad = LoadState.Idle,
            Comments = comments,
            NextComments = null,
            CommentsLoad = LoadState.Idle,
            RemoteLinks = [],
            LinksLoad = LoadState.Idle,
            LoadedAt = _now,
        };
    }

    private static IssueDetail Issue(AdfDocument? description)
    {
        return new IssueDetail(
            "1",
            "A-1",
            "A summary",
            _status,
            _type,
            "High",
            new IssueAssignee("a", "Ann Assignee"),
            new IssueAssignee("r", "Rita Reporter"),
            ["one", "two"],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            description,
            new IssueStub("A-0", "The epic", _status, _type),
            [],
            [],
            []);
    }

    private static IssueComment Comment(string author, string text)
    {
        var body = AdfParser.Parse(
            $$"""{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"{{text}}"}]}]}""");

        return new IssueComment("c", new IssueAssignee("w", author), body, _now.AddHours(-2), _now.AddHours(-2));
    }
}
