using Geeratya.Core.Keymap;

namespace Geeratya.Core.State;

/// <summary>
/// The which-key popup waits before it opens, so a fast chord such as <c>&lt;space&gt;ik</c> never flashes it.
/// The wait is
/// counted on the clock the shell hands to <see cref="Views.Tick"/>; there is no timer in the state layer.
/// </summary>
public static class WhichKeyDelay
{
    public static TimeSpan Delay { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// <paramref name="state"/> with the popup opened when keys are pending and the delay has passed; the same
    /// instance otherwise. Once open it stays open while the chord continues (a second level opens at once).
    /// </summary>
    public static AppState Opened(AppState state, DateTimeOffset now)
    {
        if (state.PendingKeys.Length == 0 || state.PendingSince is not { } since || state.WhichKeyOpenFor == since
            || now - since < Delay)
        {
            return state;
        }

        return state with { WhichKeyOpenFor = since };
    }

    /// <summary>True when the popup shows: Normal mode, keys pending, the delay passed and the switch on.</summary>
    public static bool IsOpen(AppState state)
    {
        return state is { Mode: InputMode.Normal, Settings.WhichKey: true, PendingKeys.Length: > 0 }
            && state.PendingSince is { } since
            && state.WhichKeyOpenFor == since;
    }
}
