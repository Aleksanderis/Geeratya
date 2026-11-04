using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class FlashOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenTypedText_WhenRendering_ThenTheCharacterAfterTheMatchIsReplacedByTheRowLetter()
    {
        var state = Open("Sum");

        var lines = Body(state, 100, 10);

        Assert.Contains(lines, line => line.Contains("A-1") && line.Contains("Suma"));
        Assert.Contains(lines, line => line.Contains("A-2") && line.Contains("Sums"));
        Assert.Contains(lines, line => line.Contains("A-3") && line.Contains("Sumd"));
    }

    [Fact]
    public void GivenALabelledRow_WhenApplying_ThenTheMatchIsStrongAndTheLabelIsAChipOfTheSameWidth()
    {
        var line = StyledLine.Of("ab 世界 cd");
        var label = new FlashLabel(0, 3, 4, 8, 'x');

        var drawn = FlashOverlay.Apply(line, label);

        Assert.Equal("ab 世界 xd", drawn.PlainText);
        Assert.Contains(drawn.Spans, span => span is { Text: "世界", Role: TextRole.Strong });
        Assert.Contains(drawn.Spans, span => span is { Text: "x", Role: TextRole.OverlayChip });
    }

    [Fact]
    public void GivenALabelOverAWideCharacter_WhenApplying_ThenTheLetterIsPaddedToKeepTheWidth()
    {
        var line = StyledLine.Of("a世b");
        var label = new FlashLabel(0, 0, 1, 1, 'x');

        var drawn = FlashOverlay.Apply(line, label);

        Assert.Equal("ax b", drawn.PlainText);
    }

    [Fact]
    public void GivenTheOpenPrompt_WhenRenderingTheStatusLine_ThenItShowsThePromptAndTheText()
    {
        var state = Open("Su");

        var status = StatusBar.Render(state, 100, _keymap);

        Assert.Contains("INSERT", status);
        Assert.Contains("jump to a row: Su", status);
    }

    [Fact]
    public void GivenTheFramingLimits_WhenComparedWithTheFrameGrid_ThenTheListColumnsAgree()
    {
        Assert.Equal(FrameGrid.MinFramedWidth, ListViewColumns.MinFramedWidth);
        Assert.Equal(FrameGrid.MinFramedHeight, ListViewColumns.MinFramedHeight);
    }

    private static AppState Open(string text)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Rows = [Issue(1), Issue(2), Issue(3)],
            KeyWidth = 3,
        };
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = session,
            ViewportColumns = 100,
            FlashJump = new FlashJumpState(text),
        };
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"A-{number}",
            "Summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private static List<string> Body(AppState state, int width, int height)
    {
        return FrameText.Unframe(FrameBuilder.Build(state, width, height + FrameBuilder.HeaderRows, _keymap)
            .Skip(FrameBuilder.HeaderRows));
    }
}
