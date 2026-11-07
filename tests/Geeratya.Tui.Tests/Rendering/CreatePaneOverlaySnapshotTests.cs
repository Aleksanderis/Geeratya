using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The create form as panes in the wide overlay (#708) at the sizes of the pane spec: a sidebar from 104 terminal
/// columns, one column below. To refresh after an intended change, copy the actual frame from the failure into the
/// matching file and review the diff.
/// </summary>
public class CreatePaneOverlaySnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(104, 30)]
    [InlineData(80, 24)]
    [InlineData(40, 14)]
    public void GivenACreateForm_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Open(Form()), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";

        Assert.Equal(SnapshotFile.Expect($"form-create-panes-{width}x{height}.txt", actual), actual);
    }

    [Fact]
    public void GivenAFocusedDetailsField_WhenBuildingTheNarrowFrame_ThenTheDetailsPaneIsUnderTheDescription()
    {
        var form = Form() with { Focus = 2, Navigating = true };

        var lines = FrameBuilder.Build(Open(form), 80, 30, _keymap);

        var description = lines.ToList().FindIndex(line => line.Contains("Description", StringComparison.Ordinal));
        var details = lines.ToList().FindIndex(line => line.Contains("Details", StringComparison.Ordinal));
        Assert.True(description >= 0 && details > description);
        Assert.All(lines, line => Assert.Equal(80, DisplayWidth.Of(line)));
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(104, 30)]
    [InlineData(80, 24)]
    [InlineData(64, 20)]
    [InlineData(40, 10)]
    public void GivenAnySize_WhenBuildingTheFrame_ThenEveryLineHasTheTerminalWidth(int width, int height)
    {
        foreach (var form in new[] { Form(), Form() with { Focus = 3, Navigating = true }, Form() with { Focus = 1 } })
        {
            var lines = FrameBuilder.Build(Open(form), width, height, _keymap);

            Assert.Equal(height, lines.Count);
            Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
        }
    }

    [Fact]
    public void GivenTheWideFrame_WhenReadingIt_ThenTheFourPanesAndTheRequiredMarksShow()
    {
        var lines = FrameBuilder.Build(Open(Form()), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("Create · A · Task", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("required *", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("╭─ Description", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("╭─ Details", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("╭─ Not editable here", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Parent *", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("from selection", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Team", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheWideFrame_WhenReadingIt_ThenTheDetailsPaneIsASidebarAtTheRightOfTheOthers()
    {
        var lines = FrameBuilder.Build(Open(Form()), 120, 36, _keymap);

        var title = lines.First(line => line.Contains("╭─ Create", StringComparison.Ordinal));

        Assert.True(title.IndexOf("╭─ Details", StringComparison.Ordinal) > title.IndexOf('╮'));
    }

    [Fact]
    public void GivenAMoreFieldsHint_WhenFieldsAreFolded_ThenCtrlFShowsMoreFieldsAtTheBottomOfDetails()
    {
        var lines = FrameBuilder.Build(Open(Form()), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("Ctrl-f show more fields", StringComparison.Ordinal));

        var all = FrameBuilder.Build(Open(Form() with { ShowAllFields = true }), 120, 90, _keymap);

        Assert.Contains(all, line => line.Contains("Ctrl-f fewer fields", StringComparison.Ordinal));
        Assert.Contains(all, line => line.Contains("Optional 6", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Optional 6", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnErrorAtTheSummary_WhenBuildingTheFrame_ThenTheErrorShowsUnderTheSummaryInTheTitlePane()
    {
        var form = Form();
        var fields = form.Fields.ToArray();

        fields[0] = fields[0] with { Error = "Required" };

        var lines = FrameBuilder.Build(Open(form with { Fields = fields }), 120, 36, _keymap);

        var summary = lines.ToList().FindIndex(line => line.Contains("required *", StringComparison.Ordinal));
        Assert.Contains("! Required", lines[summary + 1], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("! Summary: Required", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheNavigateState_WhenBuildingTheFrame_ThenTheFocusedBlockShowsTheStateAndTheBar()
    {
        var form = Form() with { Focus = 2, Navigating = true };

        var plain = FrameBuilder.Build(Open(form), 120, 36, _keymap);
        var styled = FrameBuilder.BuildStyled(Open(form), 120, 36, _keymap)!;
        var bars = styled.Body.SelectMany(line => line.Spans).Count(span => span.Role == TextRole.SelectionBar);

        Assert.Contains(plain, line => line.Contains("[navigate]", StringComparison.Ordinal));
        Assert.Contains(plain, line => line.Contains("Create A · Task · Parent  [navigate]", StringComparison.Ordinal));
        Assert.True(bars >= 1);
    }

    [Fact]
    public void GivenAFocusedPane_WhenCollectingSpans_ThenOnlyItsFrameIsAccent()
    {
        var focusedTitle = FrameBuilder.BuildStyled(Open(Form()), 120, 36, _keymap)!;
        var focusedDetails = FrameBuilder.BuildStyled(Open(Form() with { Focus = 2 }), 120, 36, _keymap)!;

        var titleFocus = FocusRows(focusedTitle);
        var detailsFocus = FocusRows(focusedDetails);

        Assert.NotEmpty(titleFocus);
        Assert.NotEmpty(detailsFocus);
        Assert.NotEqual(titleFocus, detailsFocus);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrame_ThenNoBoxDrawingOrBlockCharacterRemains()
    {
        var form = Form() with { Focus = 2, Navigating = true };
        var lines = FrameBuilder.Build(Open(form), 120, 36, _keymap, null, GlyphSet.Ascii);

        Assert.All(lines, line => Assert.DoesNotContain(line, character => "╭│▌▾─".Contains(character)));
    }

    [Fact]
    public void GivenAPopupOnTheFocusedField_WhenBuildingTheFrame_ThenItsRowsShowUnderTheBlockAndAreClickable()
    {
        Suggestion[] items = [new("1", "High", null, false), new("2", "Highest", null, false)];
        var source = new SuggestionSource(SuggestionKind.Options, null, []);
        var popup = new SuggestionState(3, source, "hi", items, items, 0, SuggestionStatus.Loaded, 2, null);
        var form = Form() with { Focus = 3, Suggest = popup };

        var frame = FrameBuilder.BuildStyled(Open(form), 120, 36, _keymap)!;

        Assert.Contains(frame.PlainLines(), line => line.Contains("High", StringComparison.Ordinal));
        Assert.Contains(frame.Regions, region => region.Target == MouseTarget.FormChoice && region.Index == 1);
    }

    [Fact]
    public void GivenTheFrame_WhenCollectingMouseRegions_ThenEachFieldRowFocusesItsField()
    {
        var frame = FrameBuilder.BuildStyled(Open(Form()), 120, 36, _keymap)!;

        var fields = frame.Regions.Where(region => region.Target == MouseTarget.FormField);
        var indexes = fields.Select(region => region.Index).ToList();

        Assert.Contains(0, indexes);
        Assert.Contains(1, indexes);
        Assert.Contains(2, indexes);
        Assert.Contains(frame.Regions, region => region.Target == MouseTarget.OverlayBox);
    }

    [Fact]
    public void GivenALongDescription_WhenEditingItsEnd_ThenThePaneScrollsToTheCaretAndShowsAScrollbar()
    {
        var text = string.Join('\n', Enumerable.Range(1, 40).Select(number => $"line {number}"));
        var form = Form();
        var fields = form.Fields.ToArray();

        fields[1] = fields[1].WithInitial(FormValue.OfText(text));

        var lines = FrameBuilder.Build(Open(form with { Fields = fields, Focus = 1 }), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("line 40 ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("line 1 ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains('█'));
        Assert.Contains(lines, line => line.Contains(" of 40", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenOpenAfter_WhenToggled_ThenTheTitleChipAndTheHintShowTheState()
    {
        var off = FrameBuilder.Build(Open(Form()), 160, 36, _keymap);
        var opening = Form() with { Target = Target() with { OpenAfter = true } };
        var on = FrameBuilder.Build(Open(opening), 160, 36, _keymap);

        Assert.Contains(off, line => line.Contains("Ctrl-g open after: off", StringComparison.Ordinal));
        Assert.DoesNotContain(off, line => line.Contains("opens after create", StringComparison.Ordinal));
        Assert.Contains(on, line => line.Contains("Ctrl-g open after: on", StringComparison.Ordinal));
        Assert.Contains(on, line => line.Contains("opens after create", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheTypePickerOrLoading_WhenBuildingTheFrame_ThenTheSmallBoxStillShows()
    {
        var loading = FormState.Opened(Target(), 3, null);

        var lines = FrameBuilder.Build(Open(loading), 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains("Loading fields…", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Not editable here", StringComparison.Ordinal));
    }

    private static List<int> FocusRows(StyledFrame frame)
    {
        return [.. frame.Body.Select((line, row) => (line, row))
            .Where(pair => pair.line.Spans.Any(span => span.Role == TextRole.FrameFocus))
            .Select(pair => pair.row)];
    }

    private static CreateFormTarget Target()
    {
        return new CreateFormTarget("A", "A-1", false, new CreateIssueType("10", "Task", string.Empty, 0, false));
    }

    private static AppState Open(FormState form)
    {
        return FormOverlaySnapshotTests.WithForm(form) with { ViewportColumns = 120, ViewportBodyHeight = 30 };
    }

    private static FormState Form()
    {
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, true, []);
        var description = FormField.Create("description", "Description", FormFieldKind.Paragraph, false, []);
        var parent = FormField.Create("parent", "Parent", FormFieldKind.Parent, true, [])
            .WithInitial(FormValue.OfChosen(["A-1"])) with { Options = [new FieldOption("A-1", "A-1 Checkout epic")] };
        var priority = FormField.Create(
                "priority",
                "Priority",
                FormFieldKind.Select,
                false,
                [new FieldOption("1", "High"), new FieldOption("3", "Medium")])
            .WithInitial(FormValue.OfChosen(["3"]));
        var assignee = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, []);
        var labels = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, []);
        var optional = Enumerable.Range(1, 6)
            .Select(number => FormField.Create($"opt{number}", $"Optional {number}", FormFieldKind.Text, false, []));
        var team = FormField.Create("customfield_2", "Team", FormFieldKind.Unsupported, false, []);
        FormField[] fields = [summary, description, parent, priority, assignee, labels, .. optional, team];

        return FormState.Opened(Target(), 3, null) with
        {
            Fields = fields,
            IsLoading = false,
            Navigable = true,
        };
    }
}
