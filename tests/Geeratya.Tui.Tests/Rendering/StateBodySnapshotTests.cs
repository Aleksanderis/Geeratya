using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The loading, empty, failed and board-empty bodies at the three snapshot sizes in the glyph and the ASCII tier:
/// every note starts one column inside the frame. To refresh after an intended change, run with
/// <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class StateBodySnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    public static TheoryData<string, GlyphSet, int, int> Snapshots
    {
        get
        {
            var data = new TheoryData<string, GlyphSet, int, int>();

            foreach (var name in new[] { "loading", "empty", "failed", "board-empty" })
            {
                foreach (var glyphs in new[] { GlyphSet.Unicode, GlyphSet.Ascii })
                {
                    data.Add(name, glyphs, 120, 36);
                    data.Add(name, glyphs, 80, 24);
                    data.Add(name, glyphs, 40, 10);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Snapshots))]
    public void GivenAStateBody_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(
        string name,
        GlyphSet glyphs,
        int width,
        int height)
    {
        var lines = FrameBuilder.Build(Sample(name), width, height, _keymap, NetworkStateSnapshotTests.Now, glyphs);
        var actual = string.Join('\n', lines) + "\n";
        var file = $"statebody-{name}-{glyphs.ToString().ToLowerInvariant()}-{width}x{height}.txt";

        var expected = SnapshotFile.Expect(file, actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("loading", "│ Loading…")]
    [InlineData("empty", "│ No issues match.")]
    [InlineData("failed", "│ Jira rejected the credentials.")]
    [InlineData("board-empty", "│ The board has no issues.")]
    public void GivenEachState_WhenBuildingTheFrame_ThenTheNoteStartsOneColumnInsideTheBorder(
        string name,
        string expected)
    {
        var lines = FrameBuilder.Build(Sample(name), 80, 24, _keymap, NetworkStateSnapshotTests.Now);

        Assert.Contains(lines, line => line.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFailedBody_WhenBuildingTheFrame_ThenItNamesTheRetryKeyFromTheKeymap()
    {
        var key = _keymap.FindKeyLabel(InputMode.Normal, ActionIds.ViewRefresh);
        var lines = FrameBuilder.Build(Sample("failed"), 80, 24, _keymap, NetworkStateSnapshotTests.Now);

        Assert.NotNull(key);
        Assert.Contains(lines, line => line.Contains($"│ {key} to retry", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnotherRefreshKey_WhenAskingForTheRetryText_ThenItNamesThatKey()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "F5", ActionIds.ViewRefresh)]);

        Assert.Equal("F5 to retry", StateBody.RetryText(keymap));
    }

    [Fact]
    public void GivenNoRefreshKey_WhenAskingForTheRetryText_ThenItNamesThePaletteCommand()
    {
        Assert.Equal("the palette command refresh to retry", StateBody.RetryText(new KeymapTable([])));
    }

    [Fact]
    public void GivenEachNote_WhenAskingForItsText_ThenTheWordsAreStable()
    {
        Assert.Equal("Loading…", StateBody.Loading(Glyphs.For(GlyphSet.Unicode)).PlainText.Trim());
        Assert.Equal("Loading...", StateBody.Loading(Glyphs.For(GlyphSet.Ascii)).PlainText.Trim());
        Assert.Equal(" gone", StateBody.Empty("gone").Single().PlainText);
        Assert.Equal(
            [" gone", " because"],
            StateBody.Empty("gone", "because").Select(line => line.PlainText));
        Assert.Equal(
            [" boom", $" {StateBody.RetryText(_keymap)}"],
            StateBody.Failure("boom", _keymap).Select(line => line.PlainText));
    }

    private static AppState Sample(string name)
    {
        var source = name == "board-empty" ? ViewSourceKind.Board : ViewSourceKind.Jql;
        var definition = new ViewDefinition(source, "project = GCM", ViewLayout.List, "My open issues");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with { Load = LoadState.Idle, KeyWidth = 6 };

        tab = name switch
        {
            "loading" => tab with { Load = new Loading(LoadReason.Open), RequestId = 2 },
            "failed" => tab with { Load = new Failed(JiraErrorKind.Unauthorized, "Jira rejected the credentials.") },
            _ => tab,
        };

        var session = SiteSession.Empty(_work) with { Tabs = [tab], Active = tab.Id, Link = new Online() };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = session };
    }
}
