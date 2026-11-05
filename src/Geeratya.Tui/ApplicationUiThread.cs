using Terminal.Gui.App;

namespace Geeratya.Tui;

/// <summary>
/// The UI thread of a Terminal.Gui application. Until <see cref="Attach"/> is called, or after
/// <see cref="Detach"/>, no loop owns the screen and work runs inline on the caller.
/// </summary>
public sealed class ApplicationUiThread : IUiThread
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
