namespace Geeratya.Tui;

/// <summary>Runs work on the thread that owns the screen.</summary>
public interface IUiThread
{
    /// <summary>Runs <paramref name="action"/> on the UI thread; the task ends when it has run.</summary>
    Task InvokeAsync(Action action, CancellationToken cancellationToken);
}
