using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Updates;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusBarUpdateTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly UpdateNotice _notice = new("1.3.0", "run `geeratya update`");

    [Fact]
    public void GivenANewerRelease_WhenRenderingTheStatusLine_ThenTheShortHintShowsBesideTheKeyHints()
    {
        var state = AppState.Initial with { Update = _notice };

        var line = StatusBar.Render(state, 80, _keymap);

        Assert.Contains("update 1.3.0 available", line, StringComparison.Ordinal);
        Assert.Contains("? help", line, StringComparison.Ordinal);
        Assert.Contains("q quit", line, StringComparison.Ordinal);
        Assert.Equal(80, line.Length);
    }

    [Fact]
    public void GivenNoNewerRelease_WhenRenderingTheStatusLine_ThenNothingAboutUpdatesShows()
    {
        var line = StatusBar.Render(AppState.Initial, 80, _keymap);

        Assert.DoesNotContain("update", line, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANoticeOnTheLine_WhenANewerReleaseExists_ThenTheNoticeWinsOverTheHint()
    {
        var state = AppState.Initial with { Update = _notice, Message = "Saved", Status = new StatusMessage("Saved", StatusSeverity.Info) };

        var line = StatusBar.Render(state, 80, _keymap);

        Assert.Contains("Saved", line, StringComparison.Ordinal);
        Assert.DoesNotContain("update 1.3.0", line, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANarrowTerminal_WhenRenderingTheStatusLine_ThenTheLineStaysExactlyAsWide()
    {
        var state = AppState.Initial with { Update = _notice };

        foreach (var width in new[] { 20, 40, 60 })
        {
            Assert.Equal(width, StatusBar.Render(state, width, _keymap).Length);
        }
    }
}
