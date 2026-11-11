using Geeratya.Tui.Shutdown;
using Terminal.Gui.App;

namespace Geeratya.Tui;

/// <summary>
/// The UI thread of a Terminal.Gui application. Until <see cref="Attach"/> is called, or after
/// <see cref="Detach"/>, no loop owns the screen and work runs inline on the caller.
/// </summary>
public sealed class ApplicationUiThread : IUiThread, IApplicationRelease
{
    private volatile IApplication? _application;

    public void Attach(IApplication application)
    {
        _application = application;
    }

    public void Detach()
    {
        _application = null;
    }

    /// <summary>
    /// Detaches and disposes the application, which restores the terminal. For the hard exit: it runs on another
    /// thread than the UI thread and does nothing when the application is already gone.
    /// </summary>
    public void Release()
    {
        Interlocked.Exchange(ref _application, null)?.Dispose();
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        var application = _application;

        if (application is null)
        {
            action();

            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));

        application.Invoke(() =>
        {
            try
            {
                action();
                done.TrySetResult();
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        });

        return done.Task.ContinueWith(
            task =>
            {
                registration.Dispose();

                return task;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).Unwrap();
    }
}
