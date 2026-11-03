using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Each network state (offline, rate limited, stale, error, loading) at the three snapshot sizes, as plain frames
/// (the text markers stand in for the colour tiers), and the style of each banner badge in the three colour tiers.
/// To refresh the snapshots after an intended change, run the tests with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and
/// review the diff.
/// </summary>
public class NetworkStateSnapshotTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 5, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    public static TheoryData<string, int, int> Snapshots
    {
        get
        {
            var data = new TheoryData<string, int, int>();

            foreach (var name in new[] { "offline", "ratelimited", "stale", "error", "loading" })
            {
                data.Add(name, 120, 36);
                data.Add(name, 80, 24);
                data.Add(name, 40, 10);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Snapshots))]
    public void GivenANetworkState_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(string name, int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(name), width, height, _helpLines, _keymap, _now);
        var actual = string.Join('\n', lines) + "\n";
        var file = $"network-{name}-{width}x{height}.txt";

        var expected = SnapshotFile.Expect(file, actual);

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    [Theory]
    [InlineData("offline", TextRole.Offline)]
    [InlineData("ratelimited", TextRole.RateLimited)]
    [InlineData("stale", TextRole.Stale)]
    [InlineData("error", TextRole.Error)]
    public void GivenABannerInEveryTier_WhenResolvingItsBadge_ThenItDiffersFromBodyAndNoColourRestsOnStyle(
        string name,
        TextRole role)
    {
        var frame = FrameBuilder.BuildStyled(Sample(name), 120, 36, _helpLines, _keymap, null, _now)!;
        var badge = frame.Header[1].Spans[0];

        Assert.Equal(role, badge.Role);

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16, ColorSupport.None })
        {
            var run = StyledRuns.Resolve(frame.Header[1], BuiltInThemes.Default, tier)[0];
            var body = Palette.Resolve(BuiltInThemes.Default, tier, PaletteRole.Body);

            Assert.NotEqual(body, run.Style);
        }

        var plain = StyledRuns.Resolve(frame.Header[1], BuiltInThemes.Default, ColorSupport.None)[0].Style;
        Assert.NotEqual(RoleStyle.None, plain.Style);
        Assert.False(string.IsNullOrWhiteSpace(badge.Text));
    }

    [Fact]
    public void GivenNetworkStates_WhenRenderingTheStatusLine_ThenItMirrorsTheStateInWords()
    {
        Assert.Contains("offline", StatusBar.Render(Sample("offline"), 80, _keymap, _now));
        Assert.Contains("rate limited 25s", StatusBar.Render(Sample("ratelimited"), 80, _keymap, _now));
        Assert.Contains("stale", StatusBar.Render(Sample("stale"), 80, _keymap, _now));
        Assert.Contains("error", StatusBar.Render(Sample("error"), 80, _keymap, _now));
        Assert.Contains("loading", StatusBar.Render(Sample("loading"), 80, _keymap, _now));
    }

    [Fact]
    public void GivenAPendingKeyOrNotice_WhenRenderingTheStatusLine_ThenThoseWinOverTheNetworkWord()
    {
        var withKeys = Sample("offline") with { PendingKeys = "g" };
        var withNotice = Sample("offline") with { Message = "Switched." };

        Assert.DoesNotContain("offline", StatusBar.Render(withKeys, 80, _keymap, _now));
        Assert.DoesNotContain("offline", StatusBar.Render(withNotice, 80, _keymap, _now));
    }

    [Fact]
    public void GivenNarrowSizesDownToNothing_WhenBuildingAnyNetworkFrame_ThenEveryLineFitsAndNothingThrows()
    {
        foreach (var name in new[] { "offline", "ratelimited", "stale", "error", "loading" })
        {
            for (var width = 0; width <= 45; width++)
            {
                for (var height = 0; height <= 12; height++)
                {
                    var lines = FrameBuilder.Build(Sample(name), width, height, _helpLines, _keymap, _now);

                    Assert.Equal(width == 0 ? 0 : height, lines.Count);
                    Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
                }
            }
        }
    }

    [Fact]
    public void GivenAFrameWithAndWithoutABanner_WhenBuildingBoth_ThenTheBodyKeepsItsHeight()
    {
        var fresh = FrameBuilder.BuildStyled(Sample("fresh"), 80, 24, _helpLines, _keymap, null, _now)!;
        var offline = FrameBuilder.BuildStyled(Sample("offline"), 80, 24, _helpLines, _keymap, null, _now)!;

        Assert.Equal(fresh.Body.Count, offline.Body.Count);
        Assert.Equal(string.Empty, fresh.Header[1].PlainText.Trim());
        Assert.NotEqual(string.Empty, offline.Header[1].PlainText.Trim());
    }

    private static AppState Sample(string name)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "My open issues");
        var rows = new[]
        {
            Row("GCM-12", "Fix login redirect after token refresh", "In Progress", "indeterminate"),
            Row("GCM-14", "Crash when the board has no columns", "To Do", "new"),
            Row("GCM-15", "As a user I can filter by label", "Done", "done"),
        };
        var loaded = _now - TimeSpan.FromMinutes(23);
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Rows = rows,
            KeyWidth = 6,
            SelectedKey = "GCM-12",
            Load = LoadState.Idle,
            RequestId = 0,
            LoadedAt = loaded,
            NextRefreshAt = _now + TimeSpan.FromSeconds(8),
        };
        var link = new Online() as SiteLink;

        switch (name)
        {
            case "offline":
                tab = tab with { IsStale = true, Load = new Failed(JiraErrorKind.Network, "Cannot reach Jira.") };
                link = new Offline(_now);

                break;
            case "ratelimited":
                link = new RateLimited(_now + TimeSpan.FromSeconds(25));

                break;
            case "stale":
                tab = tab with { IsStale = true, Load = new Loading(LoadReason.Open), RequestId = 2 };

                break;
            case "error":
                tab = tab with { Load = new Failed(JiraErrorKind.Unauthorized, "Jira rejected the credentials.") };
                link = new Rejected();

                break;
            case "loading":
                tab = tab with { Load = new Loading(LoadReason.Refresh), RequestId = 2 };

                break;
        }

        var session = SiteSession.Empty(_work) with { Tabs = [tab], Active = tab.Id, Link = link };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = session };
    }

    private static IssueSummary Row(string key, string summary, string status, string category)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus(status, status, category),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "GCM");
    }
}
