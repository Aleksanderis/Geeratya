using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The jump prompt at the three snapshot sizes, with a wide-character title and a long one. To refresh after an
/// intended change, copy the actual frame from the failure into the matching file and review the diff.
/// </summary>
public class JumpOverlaySnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPrompt_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("gcm"), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var name = $"jump-{width}x{height}.txt";

        Assert.Equal(SnapshotFile.Expect(name, actual), actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPrompt_WhenReadingTheRegions_ThenEachRowCoversItsKey(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample("gcm"), width, height, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.JumpRow);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains("GCM", row.Text, StringComparison.Ordinal));
        Assert.All(rows, row => Assert.InRange(row.Region.Top, FrameBuilder.HeaderRows, height - 2));
        Assert.Equal(rows.Count, rows.Select(row => row.Region.Top).Distinct().Count());
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenWheelingOverARow_ThenTheHighlightMovesAndThePromptStaysOpen()
    {
        var state = Sample(string.Empty);
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.JumpRow)[0];

        var down = RegionProbe.Click(state, frame, row.Region, MouseGesture.WheelDown)!;

        Assert.NotNull(down.State.Jump);
        Assert.Equal(3, down.State.Jump.Selected);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenClickingASecondRow_ThenThePromptClosesOnThatRow()
    {
        var state = Sample("gcm");
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.JumpRow).Single(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Contains("GCM-120", row.Text);
        Assert.Null(result.State.Jump);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenNothingToOffer_WhenBuildingTheFrame_ThenThePromptShowsTheHint()
    {
        var profile = new Profile("w", "https://w.test", "a@w.test");
        var empty = Sample(string.Empty) with { Session = SiteSession.Empty(profile) };

        var lines = FrameBuilder.Build(empty, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Type a key like GCM-12", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenANotice_WhenBuildingTheFrame_ThenTheStatusLineShowsIt()
    {
        var state = Sample("zz") with { Jump = new JumpState("zz", 0, "\"zz\" is not an issue key") };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains("is not an issue key", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 30; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                FrameBuilder.Build(Sample("gcm"), width, height, _keymap);
            }
        }
    }

    internal static AppState Sample(string text)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var tab = TabState.Opened(
            new TabId(1),
            new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "project = GCM"),
            2,
            3,
            10) with
        {
            Rows =
            [
                Row("GCM-12", "Fix the login redirect"),
                Row("GCM-120", "日本語の見出しを含む課題"),
                Row("GTM-3", "Not shown when typing gcm"),
                Row("GCM-7", "A rather long summary that cannot possibly fit into the narrow box of the prompt"),
            ],
        };

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = SiteSession.Empty(profile) with
            {
                Tabs = [tab],
                Active = tab.Id,
                RecentKeys = ["GCM-4", "GTM-9"],
            },
            Jump = new JumpState(text, 0, null),
        };
    }

    private static IssueSummary Row(string key, string summary)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "GCM");
    }
}
