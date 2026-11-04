using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs the history effects (read, remember, clear the profile's history file). A file that cannot be written is
/// ignored: history is a convenience and never a reason to fail or to show an error.
/// </summary>
public sealed class HistoryEffectHandler : ILocalEffectHandler
{
    private readonly HistoryStore _store;
    private readonly object _gate = new();
    private long _version;

    public HistoryEffectHandler(HistoryStore store)
    {
        _store = store;
    }

    public bool CanRun(Effect effect)
    {
        return effect is LoadHistoryEffect or RecordHistoryEffect or ClearHistoryEffect;
    }

    public Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        return Task.FromResult(Run(effect));
    }

    /// <summary>The command that reports the stored history, or null when the effect is not a history effect.</summary>
    public Command? Run(Effect effect)
    {
        try
        {
            lock (_gate)
            {
                return effect switch
                {
                    LoadHistoryEffect load => Reply(load.Profile, _store.Load(load.Profile)),
                    RecordHistoryEffect record => Record(record),
                    ClearHistoryEffect clear => Clear(clear),
                    _ => null,
                };
            }
        }
        catch (Exception)
        {
            // History is a convenience: whatever goes wrong with the file, the application carries on.
            return null;
        }
    }

    private HistoryLoaded Reply(Profile profile, SiteHistory history)
    {
        return new HistoryLoaded(profile, history, ++_version);
    }

    private HistoryLoaded Record(RecordHistoryEffect record)
    {
        var history = _store.Update(
            record.Profile,
            current => record.Kind == HistoryKind.Query ? current.WithQuery(record.Text) : current.WithIssue(record.Text));

        return Reply(record.Profile, history);
    }

    private HistoryLoaded Clear(ClearHistoryEffect clear)
    {
        _store.Delete(clear.Profile);

        return Reply(clear.Profile, SiteHistory.Empty);
    }
}
