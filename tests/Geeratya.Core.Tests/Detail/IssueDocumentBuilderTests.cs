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
        Assert.True(document.Lines.Count > 15);
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

        var lines = Texts(IssueDocumentBuilder.Build(Loaded(description, []) with { Expanded = [DetailFieldIds.Description] }, 80));

        var heading = lines.FindIndex(line => line.StartsWith("╭─ Description "));
        Assert.True(heading > 0);
        Assert.Equal(expected.Select(text => text.TrimEnd()), lines.Skip(heading + 1).Take(expected.Count));
    }

    [Fact]
    public void GivenHeader_WhenBuilding_ThenOnlyFieldsTheEditMetadataAllowsCarryTheDropdownMark()
    {
        var detail = Loaded(null, []) with { EditableFields = [DetailFieldIds.Priority, DetailFieldIds.Labels] };
        var texts = Texts(IssueDocumentBuilder.Build(detail, 90));

        Assert.Contains("Status  moves via transition", texts);
        Assert.Contains(" ● In Progress ▾", texts);
        Assert.Contains(" ▲ High ▾", texts);
        Assert.Contains("  AA  Ann Assignee", texts);
        Assert.Contains("  RR  Rita Reporter", texts);
        Assert.DoesNotContain(texts, text => text.Contains("Rita Reporter ▾"));
        Assert.DoesNotContain(texts, text => text.Contains("Ann Assignee ▾"));
    }

    [Fact]
    public void GivenEditMetadataNotLoadedYet_WhenBuilding_ThenOnlyTheStatusCarriesTheDropdownMark()
    {
        var marked = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 90)).Where(text => text.EndsWith('▾')).ToList();

        Assert.Equal([" ● In Progress ▾"], marked);
    }

    [Fact]
    public void GivenAnyWidth_WhenBuilding_ThenEveryFieldHasABlockOfItsOwn()
    {
        var wide = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 120));
        var narrow = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 50));

        Assert.Contains("Status  moves via transition", wide);
        Assert.Contains("Priority", wide);
        Assert.DoesNotContain(wide, line => line.Contains("Status") && line.Contains("Priority"));
        Assert.Contains("Status  moves via transition", narrow);
        Assert.Contains("Priority", narrow);
    }

    [Fact]
    public void GivenWideLayout_WhenBuilding_ThenDetailsIsASidebarAndNotInTheMainColumn()
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []), 120);

        Assert.NotNull(document.Side);
        Assert.Equal(DetailLayout.SidebarWidthFor(document.Width), document.Side.Width);
        Assert.DoesNotContain(PaneIds.Details, document.SectionIds);
        Assert.Equal([PaneIds.Details], document.Side.SectionIds);
        Assert.All(document.Lines, line => Assert.Equal(DetailLayout.MainWidth(120), DisplayWidth.Of(line.PlainText)));
        Assert.All(document.Side.Lines, line => Assert.Equal(document.Side.Width, DisplayWidth.Of(line.PlainText)));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(80)]
    [InlineData(50)]
    public void GivenNarrowLayout_WhenBuilding_ThenDetailsSitsUnderTheMainPanes(int width)
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, [Comment("Eve", "Text")]), width);

        Assert.Null(document.Side);
        Assert.Equal(PaneIds.Details, document.SectionIds[^1]);
        Assert.Equal(PaneIds.Comments, document.SectionIds[^2]);
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    public void GivenTheSidebarThreshold_WhenAskingForASidebar_ThenItStartsAtOneHundredColumns(int width, bool expected)
    {
        Assert.Equal(expected, DetailLayout.HasSidebar(width));
        Assert.Equal(expected, IssueDocumentBuilder.Build(Loaded(null, []), width).Side is not null);
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

        var lines = Texts(IssueDocumentBuilder.Build(detail, 60));

        Assert.Contains(lines, line => line.Contains("Sprint 2 (active); earlier: Sprint 1"));
        Assert.Contains(" 5 ▾", lines);
        Assert.Contains("Estimate", lines);
        Assert.Contains(" A-0 Onboarding", lines);
        Assert.DoesNotContain("Parent", lines);
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

    [Theory]
    [InlineData(null, "Due none")]
    [InlineData("2026-12-31", "Due 2026-12-31")]
    public void GivenLoadedIssue_WhenBuilding_ThenTheDetailsPaneShowsTheDueDateOrNone(string? due, string expected)
    {
        var detail = Loaded(null, []);
        detail = detail with
        {
            Issue = detail.Issue! with { DueDate = due is null ? null : new DateOnly(2026, 12, 31) },
        };

        var text = string.Join('\n', Texts(IssueDocumentBuilder.Build(detail, 140)));

        Assert.Contains(expected, text);
        Assert.Contains("· Updated", text);
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
    public void GivenMissingValues_WhenBuilding_ThenReadOnlyFieldsAreOmittedAndTheOthersShowAPlaceholder()
    {
        var detail = Loaded(null, []) with
        {
            Issue = Issue(null) with { Priority = null, Reporter = null, Assignee = null, Labels = [] },
            EditableFields = [DetailFieldIds.Priority, DetailFieldIds.Assignee, DetailFieldIds.Labels],
        };

        var texts = Texts(IssueDocumentBuilder.Build(detail, 120));

        Assert.DoesNotContain("Reporter", texts);
        Assert.Contains(" None ▾", texts);
        Assert.Contains(" [--] Unassigned ▾", texts);
        Assert.Contains(" none ▾", texts);
    }

    [Fact]
    public void GivenVeryLongLabelList_WhenBuilding_ThenTheChipsWrapInsteadOfBeingCut()
    {
        var labels = Enumerable.Range(1, 40).Select(number => $"label-number-{number}").ToArray();
        var detail = Loaded(null, []) with { Issue = Issue(null) with { Labels = labels } };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 60));

        Assert.Contains(lines, line => line.StartsWith("  label-number-1   label-number-2 "));
        Assert.Contains(lines, line => line.Contains("label-number-40"));
        Assert.All(IssueDocumentBuilder.Build(detail, 60).Lines, line => Assert.Equal(60, DisplayWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenCommentEditedLongAfterWritingAndOneWithoutBodyOrAuthor_WhenBuilding_ThenTheMarkersShow()
    {
        var edited = Comment("Eve", "Text") with { Updated = _now.AddHours(-1) };
        var empty = new IssueComment("c9", null, AdfDocument.Empty, _now.AddMinutes(-5), _now.AddMinutes(-5));

        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, [edited, empty]), 80));

        Assert.Contains(lines, line => line.Contains("Eve") && line.Contains("2 h ago · edited"));
        Assert.Contains(lines, line => line.Contains("Unknown") && line.Contains("5 min ago"));
        Assert.Contains(lines, line => line.Contains("(no text)"));
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

        Assert.StartsWith("A-1", Title(IssueDocumentBuilder.Build(detail, 80), 0));
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
    public void GivenLoadedIssueWithoutRelations_WhenBuilding_ThenEveryPaneGetsAFrameOfItsOwn()
    {
        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, [Comment("Eve", "Text")]), 80));

        var document = IssueDocumentBuilder.Build(Loaded(null, [Comment("Eve", "Text")]), 80);

        // Every pane carries a rounded frame of its own: a top edge and a bottom edge each.
        Assert.Equal(
            ["A-1", "Description", "Comments", "Details"],
            document.PaneTitles.Select((_, index) => Title(document, index).Split(' ')[0]));
        Assert.Equal(4, lines.Count(line => line.StartsWith('╭')));
        Assert.Equal(4, lines.Count(line => line.StartsWith('╰')));
        Assert.Null(document.Side);
        Assert.DoesNotContain(lines, line => line.StartsWith('├'));
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
        var main = DetailLayout.HasSidebar(width) ? DetailLayout.MainWidth(width) : width;

        Assert.All(document.Lines, line => Assert.Equal(main, DisplayWidth.Of(line.PlainText)));
        Assert.All(document.Side?.Lines ?? [], line => Assert.Equal(document.Side!.Width, DisplayWidth.Of(line.PlainText)));
    }

    [Fact]
    public void GivenHeaderPanel_WhenBuilding_ThenTheTitleHoldsKeyAndTypeAndTheSummaryIsInside()
    {
        var lines = Texts(IssueDocumentBuilder.Build(Loaded(null, []), 80));

        Assert.Equal("A-1 · Story", Title(IssueDocumentBuilder.Build(Loaded(null, []), 80), 0));
        Assert.StartsWith("A summary", lines[1]);
        Assert.EndsWith("In Progress", lines[1].TrimEnd());
    }

    [Fact]
    public void GivenALongParent_WhenBuilding_ThenEachFieldKeepsItsOwnBlock()
    {
        var detail = Loaded(null, []) with
        {
            Issue = Issue(null) with { Parent = new IssueStub("A-0", new string('x', 60), _status, _type) },
        };

        var lines = Texts(IssueDocumentBuilder.Build(detail, 100));

        Assert.Contains("Status  moves via transition", lines);
        Assert.Contains(lines, line => line.StartsWith(" A-0 xxxx") && line.EndsWith('…'));
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

    [Theory]
    [InlineData(24, 8)]
    [InlineData(36, 12)]
    [InlineData(90, 12)]
    public void GivenNoDescription_WhenBuilding_ThenThePaneKeepsTheRoomOfTheViewport(int viewport, int bodyRows)
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []), 80, viewport);

        Assert.Equal(bodyRows + 2, PaneRows(document, PaneIds.Description));
    }

    [Fact]
    public void GivenAOneLineDescription_WhenBuilding_ThenThePaneIsPaddedToTheSameHeightAsAnEmptyOne()
    {
        var empty = IssueDocumentBuilder.Build(Loaded(null, []), 80, 36);
        var oneLine = IssueDocumentBuilder.Build(Loaded(AdfMarkdownParser.Parse("one line"), []), 80, 36);

        Assert.Equal(PaneRows(empty, PaneIds.Description), PaneRows(oneLine, PaneIds.Description));
    }

    [Fact]
    public void GivenAPaddedDescription_WhenMovingDown_ThenTheCursorSkipsTheBlankRows()
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []), 80, 36);
        var start = document.Sections[document.SectionIds.ToList().IndexOf(PaneIds.Description)];

        Assert.Contains("No description.", document.Lines[start].PlainText);
        Assert.All(Enumerable.Range(start + 1, 3), line => Assert.DoesNotContain(line, document.Stops));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(12)]
    public void GivenNoViewportOrASmallOne_WhenBuilding_ThenTheEmptyDescriptionStaysOneRow(int? viewport)
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []), 80, viewport);

        Assert.Equal(1 + 2, PaneRows(document, PaneIds.Description));
    }

    [Fact]
    public void GivenALongDescriptionAndATallViewport_WhenBuilding_ThenItIsCutAfterTheTextRowsWithTheNote()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 40).Select(number => $"line {number}"));

        var document = IssueDocumentBuilder.Build(Loaded(AdfMarkdownParser.Parse(text), []), 80, 36);

        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Description);
        Assert.Equal(12 + 2, PaneRows(document, PaneIds.Description));
        Assert.Equal(11, document.PaneScrolls[pane]!.Viewport);
        Assert.Contains("more lines", document.Lines[document.Sections[pane] + 11].PlainText);
    }

    [Theory]
    [InlineData(PaneIds.Description)]
    [InlineData(PaneIds.Comments)]
    public void GivenAZoomedShortPane_WhenBuilding_ThenItIsNotPadded(string zoomed)
    {
        var document = IssueDocumentBuilder.Build(Loaded(null, []) with { Zoomed = zoomed }, 80, 36);

        Assert.Equal(1 + 2, PaneRows(document, zoomed));
    }

    private static int PaneRows(DetailDocument document, string id)
    {
        var index = document.SectionIds.ToList().IndexOf(id);
        var next = index + 1 < document.PaneStarts.Count ? document.PaneStarts[index + 1] : document.Lines.Count;

        return next - document.PaneStarts[index];
    }

    private static string Title(DetailDocument document, int pane)
    {
        return string.Concat(document.PaneTitles[pane].Select(static span => span.Text));
    }

    /// <summary>The lines of the main column, then those of the sidebar when there is one.</summary>
    private static List<string> Texts(DetailDocument document)
    {
        return [.. document.Lines.Concat(document.Side?.Lines ?? []).Select(static line => Inside(line.PlainText))];
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
