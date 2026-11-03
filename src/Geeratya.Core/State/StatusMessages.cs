using Geeratya.Core.Keymap;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>
/// The one channel for notices (<c>docs/ux.md</c>, "Status bar"). Reducers keep posting through
/// <see cref="AppState.Message"/> (and <see cref="AppState.MessageSeverity"/> for a warning or error); the app reducer
/// then settles the result here: a new text becomes <see cref="AppState.Status"/> and enters the history, the same
/// text while it shows is ignored, a reducer that clears <see cref="AppState.Message"/> (a key press used to) changes
/// nothing, and a tick ends the message once its lifetime has passed. Pure: time comes from the command.
/// </summary>
public static class StatusMessages
{
    /// <summary>How many messages the history keeps.</summary>
    public const int HistoryLimit = 50;

    /// <summary>The column where the status line's text starts in Normal mode: the badge <c> NORMAL </c> before it.</summary>
    public const int TextStart = 8;

    /// <summary>The cells of the showing message in the status line (Normal mode only), null when none shows.</summary>
    public static TabButton? Cells(AppState state)
    {
        if (state.Mode != InputMode.Normal || state.Status is not { } status || Echoing(state))
        {
            return null;
        }

        return new TabButton(TextStart, TextStart + DisplayWidth.Of(status.Line));
    }

    /// <summary>True while the status line echoes what the user types (pending keys, a count, a rank pickup) instead of the message.</summary>
    public static bool Echoing(AppState state)
    {
        if (state.PendingKeys.Length > 0 || state.Count > 0)
        {
            return true;
        }

        return state.Session.Active is { } id && state.Session.Tabs.Any(tab => tab.Id == id && tab.RankPickup is not null);
    }

    public static TimeSpan LifetimeOf(StatusSeverity severity)
    {
        return severity switch
        {
            StatusSeverity.Error => TimeSpan.FromSeconds(30),
            StatusSeverity.Warning => TimeSpan.FromSeconds(15),
            _ => TimeSpan.FromSeconds(5),
        };
    }

    public static string PrefixOf(StatusSeverity severity)
    {
        return severity switch
        {
            StatusSeverity.Error => "Error: ",
            StatusSeverity.Warning => "Warning: ",
            _ => string.Empty,
        };
    }

    /// <summary>Posts <paramref name="text"/> as a warning (the settle step turns it into a message).</summary>
    public static AppState Warn(AppState state, string text)
    {
        return state with { Message = text, MessageSeverity = StatusSeverity.Warning };
    }

    /// <summary>Posts <paramref name="text"/> as an error.</summary>
    public static AppState Fail(AppState state, string text)
    {
        return state with { Message = text, MessageSeverity = StatusSeverity.Error };
    }

    /// <summary>The message ends now (<c>Esc</c>, <c>&lt;space&gt;x</c>); it stays in the history.</summary>
    public static AppState Dismiss(AppState state)
    {
        return state.Status is null && state.Message is null ? state : state with { Status = null, Message = null };
    }

    /// <summary>Opens the history overlay (it reuses the help overlay's mode and scroll).</summary>
    public static AppState OpenHistory(AppState state)
    {
        return state with
        {
            Mode = InputMode.Help,
            MessagesOpen = true,
            HelpScroll = 0,
            PendingKeys = string.Empty,
            Count = 0,
            PaletteText = string.Empty,
        };
    }

    /// <summary>
    /// Folds the post, the stamp and the expiry into <paramref name="state"/>; <paramref name="now"/> is null when the
    /// command carries no time.
    /// </summary>
    public static AppState Settle(AppState state, DateTimeOffset? now)
    {
        var settled = Posted(state, now);

        if (settled.Status is not { } status)
        {
            return settled;
        }

        if (status.PostedAt is null && now is { } stamp)
        {
            status = status with { PostedAt = stamp };
            settled = settled with { Status = status };
        }

        if (status.PostedAt is { } posted && now is { } at && at - posted >= LifetimeOf(status.Severity))
        {
            return settled with { Status = null, Message = null };
        }

        return settled;
    }

    private static AppState Posted(AppState state, DateTimeOffset? now)
    {
        var text = state.Message;

        if (text is null)
        {
            // Nothing posted. A reducer that wiped the text (a key press) does not end the message.
            if (state.Status is { } shown)
            {
                return state with { Message = shown.Text };
            }

            return state.MessageSeverity == StatusSeverity.Info ? state : state with { MessageSeverity = StatusSeverity.Info };
        }

        if (state.Status is { } current && current.Text == text)
        {
            return state.MessageSeverity == StatusSeverity.Info ? state : state with { MessageSeverity = StatusSeverity.Info };
        }

        var posted = new StatusMessage(text, state.MessageSeverity, now);
        var history = new List<StatusMessage>(Math.Min(HistoryLimit, state.StatusHistory.Count + 1)) { posted };

        history.AddRange(state.StatusHistory.Take(HistoryLimit - 1));

        return state with { Status = posted, StatusHistory = history, MessageSeverity = StatusSeverity.Info };
    }
}
