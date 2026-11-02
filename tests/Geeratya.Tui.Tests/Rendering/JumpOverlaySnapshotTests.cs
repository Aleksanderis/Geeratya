using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
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
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAnOpenPrompt_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample("gcm"), width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"jump-{width}x{height}.txt");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void GivenNothingToOffer_WhenBuildingTheFrame_ThenThePromptShowsTheHint()
    {
        var empty = Sample(string.Empty) with { Session = SiteSession.Empty(new Profile("w", "https://w.test", "a@w.test")) };

        var lines = FrameBuilder.Build(empty, 80, 24, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Type a key like GCM-12", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenANotice_WhenBuildingTheFrame_ThenTheStatusLineShowsIt()
    {
        var state = Sample("zz") with { Jump = new JumpState("zz", 0, "\"zz\" is not an issue key") };

        var lines = FrameBuilder.Build(state, 80, 24, _helpLines, _keymap);

        Assert.Contains("is not an issue key", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 30; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                FrameBuilder.Build(Sample("gcm"), width, height, _helpLines, _keymap);
            }
        }
    }

    private static AppState Sample(string text)
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
            Session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id, RecentKeys = ["GCM-4", "GTM-9"] },
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
