using Geeratya.Core.Startup;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs the remembered-tabs effects. A read answers with <see cref="TabsLoaded"/>. Saves are queued in the order the
/// reducer asked and only the latest waiting one is written, off the calling thread, so a burst of tab changes costs
/// one write and an older state never overwrites a newer one.
/// </summary>
public sealed class StartupEffectHandler
{
    private static readonly TimeSpan _flushWait = TimeSpan.FromSeconds(2);

    private readonly RememberedTabsStore _store;
    private readonly object _gate = new();
    private SaveTabsEffect? _waiting;
    private Task _writer = Task.CompletedTask;
    private bool _writing;

    public StartupEffectHandler(RememberedTabsStore store)
    {
        _store = store;
    }

    /// <summary>Raised when a save fails (not on the calling thread); the host posts the notice.</summary>
    public event Action<string>? SaveFailed;

    /// <summary>The reply of a read effect, or null when the effect is not one.</summary>
    public Command? Run(Effect effect)
    {
        if (effect is not LoadTabsEffect load)
        {
            return null;
        }

        // A save still queued (a profile edit reconnects at once) must reach the file before it is read.
        Flush();

        return new TabsLoaded(load.Profile, _store.Load(load.Profile));
    }

    /// <summary>Queues a save and returns at once.</summary>
    public void Queue(SaveTabsEffect save)
    {
        lock (_gate)
        {
            _waiting = save;

            if (_writing)
            {
                return;
            }

            _writing = true;
            _writer = Task.Run(Drain);
        }
    }

    /// <summary>Waits until the queued saves are written (at quit).</summary>
    public void Flush()
    {
        Task writer;

        lock (_gate)
        {
            writer = _writer;
        }

        writer.Wait(_flushWait);
    }

    private void Drain()
    {
        while (true)
        {
            SaveTabsEffect save;

            lock (_gate)
            {
                if (_waiting is null)
                {
                    _writing = false;

                    return;
                }

                save = _waiting;
                _waiting = null;
            }

            Write(save);
        }
    }

    private void Write(SaveTabsEffect save)
    {
        try
        {
            _store.Save(save.Profile, save.Tabs);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke($"Could not save the open tabs: {exception.Message}");
        }
    }
}
