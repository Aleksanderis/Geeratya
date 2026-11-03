using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class WhichKeyReducerTests
{
    private static readonly DateTimeOffset _start = DateTimeOffset.UnixEpoch + TimeSpan.FromHours(1);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAPrefixKey_WhenTicksPass_ThenThePopupOpensAt250MsAndNotBefore()
    {
        var state = Key(AppState.Initial, "Space", 0);

        Assert.False(WhichKeyDelay.IsOpen(Tick(state, 249)));
        Assert.True(WhichKeyDelay.IsOpen(Tick(state, 250)));
    }

    [Fact]
    public void GivenASecondChordKey_WhenItArrives_ThenTheDelayStillCountsFromTheFirstKey()
    {
        var state = Key(Key(AppState.Initial, "Space", 0), "i", 100);

        Assert.Equal(_start, state.PendingSince);
        Assert.True(WhichKeyDelay.IsOpen(Tick(state, 250)));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenTheChordCompletes_ThenTheTimingIsForgotten()
    {
        var open = Tick(Key(Key(AppState.Initial, "Space", 0), "i", 10), 300);
        var done = Key(open, "o", 310);

        Assert.Equal(string.Empty, done.PendingKeys);
        Assert.Null(done.PendingSince);
        Assert.Null(done.WhichKeyOpenFor);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenEscCancels_ThenTheTimingIsForgotten()
    {
        var open = Tick(Key(AppState.Initial, "Space", 0), 300);
        var cancelled = Key(open, "Esc", 310);

        Assert.Equal(string.Empty, cancelled.PendingKeys);
        Assert.Null(cancelled.PendingSince);
        Assert.Null(cancelled.WhichKeyOpenFor);
        Assert.False(WhichKeyDelay.IsOpen(cancelled));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenANewChordStarts_ThenItStartsClosed()
    {
        var open = Tick(Key(AppState.Initial, "Space", 0), 300);
        var done = Key(Key(Key(open, "i", 310), "k", 320), "Esc", 330);
        var next = Key(done, "g", 1000);

        Assert.False(WhichKeyDelay.IsOpen(next));
        Assert.False(WhichKeyDelay.IsOpen(Tick(next, 1100)));
        Assert.True(WhichKeyDelay.IsOpen(Tick(next, 1250)));
    }

    [Fact]
    public void GivenWhichKeyOff_WhenTheDelayPasses_ThenThePopupNeverOpens()
    {
        var off = AppState.Initial with { Settings = new AppSettings(WhichKey: false) };

        Assert.False(WhichKeyDelay.IsOpen(Tick(Key(off, "Space", 0), 5000)));
    }

    private AppState Key(AppState state, string key, int ms)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _start + TimeSpan.FromMilliseconds(ms))).State;
    }

    private AppState Tick(AppState state, int ms)
    {
        return _reducer.Reduce(state, new Tick(_start + TimeSpan.FromMilliseconds(ms))).State;
    }
}
