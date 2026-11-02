using Geeratya.Core.History;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs the history effects (read, remember, clear the profile's history file). A file that cannot be written is
/// ignored: history is a convenience and never a reason to fail or to show an error.
/// </summary>
public sealed class HistoryEffectHandler
{
    private readonly HistoryStore _store;

    public HistoryEffectHandler(HistoryStore store)
    {
        _store = store;
    }

    /// <summary>The command that reports the stored history, or null when the effect is not a history effect.</summary>
    public Command? Run(Effect effect)
    {
        try
        {
            return effect switch
            {
                LoadHistoryEffect load => new HistoryLoaded(load.Profile, _store.Load(load.Profile)),
                RecordHistoryEffect record => Record(record),
                ClearHistoryEffect clear => Clear(clear),
                _ => null,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private HistoryLoaded Record(RecordHistoryEffect record)
    {
        var history = _store.Update(
            record.Profile,
            current => record.Kind == HistoryKind.Query ? current.WithQuery(record.Text) : current.WithIssue(record.Text));

        return new HistoryLoaded(record.Profile, history);
    }

    private Command? Clear(ClearHistoryEffect clear)
    {
        _store.Delete(clear.Profile);

        return null;
    }
}
