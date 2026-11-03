using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The sprint switcher at the three snapshot sizes. To refresh after an intended change, run the tests with
/// <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class SprintPickerSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _day = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenSwitcher_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var actual = string.Join('\n', FrameBuilder.Build(Sample(12), width, height, _keymap)) + "\n";
        var expected = SnapshotFile.Expect($"sprint-picker-{width}x{height}.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GivenSprintsStillLoading_WhenBuildingTheFrame_ThenTheSwitcherSaysLoading()
    {
        var state = Sample(0) with { Picker = Picker(null, null, 10) };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("Loading…", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Switch sprint", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFailedLoad_WhenBuildingTheFrame_ThenTheErrorShows()
    {
        var state = Sample(0) with { Picker = Picker(null, "Jira said no.", 10) };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("! Jira said no.", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnOpenSwitcher_WhenBuildingTheFrame_ThenHeadingsAndTheClosedDateShow()
    {
        var lines = FrameBuilder.Build(Sample(3), 100, 30, _keymap);

        Assert.Contains(lines, line => line.Contains("Active", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Future", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Closed", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("closed 22 Oct", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("new tab", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnOpenSwitcher_WhenClickingTheMoreRow_ThenAnotherPageOfClosedSprintsShows()
    {
        var opened = Sample(25);
        var state = opened with { Picker = opened.Picker! with { Selected = 12 } };
        var frame = FrameBuilder.BuildStyled(state, 100, 40, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.PickerRow).Single(candidate => candidate.Text.Contains("more closed"));

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.NotNull(result.State.Picker);
        Assert.Equal(20, result.State.Picker!.Sprint!.ClosedShown);
    }

    [Fact]
    public void GivenAnOpenSwitcher_WhenClickingASprintRow_ThenItOpensInANewSprintTab()
    {
        var state = Sample(3);
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.PickerRow).Single(candidate => candidate.Text.Contains("Future F1"));

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Null(result.State.Picker);
        Assert.Equal("7:101", result.State.Session.Tabs[^1].Definition.SourceValue);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheAsciiGlyphSet_WhenBuildingTheFrameWithTheSwitcher_ThenNoShellGlyphIsDrawn(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(3), width, height, _keymap, null, GlyphSet.Ascii);

        Assert.All(lines, line => Assert.DoesNotContain(line, character => Glyphs.AsciiSwapped.Contains(character)));
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }

    private static SourcePickerState Picker(IReadOnlyList<Sprint>? sprints, string? error, int closedShown)
    {
        var data = new SprintPickerData(7, new TabId(1), sprints, error, closedShown);

        return new SourcePickerState(string.Empty, 0, PickerScope.Sprints, false, null) { Sprint = data };
    }

    private static AppState Sample(int closed)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var sprints = new List<Sprint>
        {
            new(100, "Sprint 12 日本語", SprintState.Active, null, _day, _day.AddDays(14), null, 7),
            new(101, "Future F1", SprintState.Future, null, null, null, null, 7),
        };

        for (var number = 1; number <= closed; number++)
        {
            var finished = _day.AddDays(number);

            sprints.Add(new Sprint(number, $"Sprint {number}", SprintState.Closed, null, _day, finished, finished, 7));
        }

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = SiteSession.Empty(profile),
            Picker = Picker(sprints, null, SprintChoices.ClosedPage),
        };
    }
}
