namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Guards every hand-off of work to the UI thread from other threads (timers, Jira replies, drag scrolling). Once the
/// application is going away (<see cref="Close"/>), work is dropped; a call that still races with the disposal and
/// throws is swallowed, because an exception on a timer or pool thread would abort the process with a stack trace
/// instead of a clean exit (#940).
/// </summary>
public sealed class UiInvokeGate
{
    private volatile bool _closed;

    public bool IsClosed => _closed;

    /// <summary>No more work reaches the application: it is about to be disposed.</summary>
    public void Close()
    {
        _closed = true;
    }

    /// <summary>Runs <paramref name="invoke"/> unless closed; false when the work was dropped.</summary>
    public bool TryInvoke(Action invoke)
    {
        if (_closed)
        {
            return false;
        }

        try
        {
            invoke();

            return true;
        }
        catch (Exception exception) when (exception is ObjectDisposedException
            or InvalidOperationException
            or NullReferenceException)
        {
            return false;
        }
    }
}
