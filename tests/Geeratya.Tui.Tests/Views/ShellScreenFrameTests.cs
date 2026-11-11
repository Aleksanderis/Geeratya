using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;
using Geeratya.Tui.Views;
using Size = System.Drawing.Size;

namespace Geeratya.Tui.Tests.Views;

public class ShellScreenFrameTests : IDisposable
{
    private readonly ShellScreen _screen = new(
        DefaultKeymap.Load(),
        ColorSupport.TrueColor,
        new Size(80, 24),
        BuiltInThemes.Default);

    [Fact]
    public void GivenABurstOfRenders_WhenTheFrameIsNeeded_ThenItIsBuiltOnce()
    {
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < 50; index++)
        {
            _screen.Render(AppState.Initial, now);
        }

        Assert.Equal(0, _screen.FramesBuilt);

        _screen.BuildFrame();
        _screen.BuildFrame();

        Assert.Equal(1, _screen.FramesBuilt);
    }

    [Fact]
    public void GivenARenderAfterABuild_WhenTheFrameIsNeeded_ThenItIsBuiltAgain()
    {
        _screen.Render(AppState.Initial, DateTimeOffset.UtcNow);
        _screen.BuildFrame();
        _screen.Render(AppState.Initial, DateTimeOffset.UtcNow);
        _screen.BuildFrame();

        Assert.Equal(2, _screen.FramesBuilt);
    }

    [Fact]
    public void GivenPendingRenders_WhenTheHitRegionsAreRead_ThenOneFrameIsBuiltForThem()
    {
        _screen.Render(AppState.Initial, DateTimeOffset.UtcNow);
        _screen.Render(AppState.Initial, DateTimeOffset.UtcNow);

        _screen.BuildFrame();
        _ = _screen.OverlayRegions;
        _ = _screen.RowMenuMarker;

        Assert.Equal(1, _screen.FramesBuilt);
    }

    public void Dispose()
    {
        _screen.Dispose();
        GC.SuppressFinalize(this);
    }
}
