using Geeratya.Core.State;

namespace Geeratya.Core.Updates;

/// <summary>The update hint: asks for a check after the first frame and keeps what the check found. Pure.</summary>
public static class UpdateReducer
{
    /// <summary>Starts the background check, as the setting allows.</summary>
    public static ReduceResult Requested(AppState state)
    {
        return new ReduceResult(state, [new CheckForUpdateEffect(state.Settings.UpdateCheck)]);
    }

    /// <summary>Keeps the notice for the status bar and posts the full sentence once.</summary>
    public static ReduceResult Available(AppState state, UpdateAvailable available)
    {
        if (state.Update == available.Notice)
        {
            return new ReduceResult(state, []);
        }

        return new ReduceResult(state with { Update = available.Notice, Message = available.Notice.Sentence }, []);
    }
}
