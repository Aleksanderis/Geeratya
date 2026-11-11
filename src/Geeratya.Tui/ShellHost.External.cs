using Geeratya.Core.Comments;
using Geeratya.Platform.Editors;
using Terminal.Gui.Views;

namespace Geeratya.Tui;

// The outside editor (#286): the terminal is handed over, so the Terminal.Gui application is disposed (alternate
// screen and input modes restored, input thread ended: the editor is the only reader of the keyboard) and a new one
// runs the same screen again afterwards. Work posted by background threads meanwhile is queued and replayed in order.
public sealed partial class ShellHost
{
    private readonly IExternalEditor _editor = ExternalEditors.ForProcess();
    private readonly Lock _invokeLock = new();
    private readonly PausableWorkGate _held = new();
    private EditExternallyEffect? _pendingEdit;

    /// <summary>Runs the screen until the user quits; each outside edit stops the loop and starts it again.</summary>
    private void RunLoop(Window screen)
    {
        _application!.Run(screen);

        while (_pendingEdit is not null && !_quitting)
        {
            RunExternalEditor(_pendingEdit);
            _application!.Run(screen);
        }
    }

    /// <summary>Thread-safe: runs the work on the UI thread, or holds it while an outside editor owns the terminal.</summary>
    private void InvokeOnUi(Action work)
    {
        lock (_invokeLock)
        {
            if (!_held.TryHold(work))
            {
                InvokeGuarded(work);
            }
        }
    }

    // Dropped when the application is not there yet, going away or already disposed (#940).
    private void InvokeGuarded(Action work)
    {
        var application = _application;

        if (application is not null)
        {
            _uiGate.TryInvoke(() => application.Invoke(work));
        }
    }

    /// <summary>On the UI thread: pauses background work first, so nothing reaches the application that is stopping.</summary>
    private void AskForExternalEdit(EditExternallyEffect edit)
    {
        lock (_invokeLock)
        {
            _held.Pause();
        }

        _pendingEdit = edit;
        _application!.Invoke(_application.RequestStop);
    }

    private void RunExternalEditor(EditExternallyEffect edit)
    {
        _pendingEdit = null;
        _stalls?.Suspend();
        _uiThread.Detach();
        _application!.Dispose();

        var result = Edit(edit.Text);

        OpenApplication();

        var finished = new ExternalEditFinished(edit.Tab, edit.Key, edit.CommentId, result.Text, result.Error);

        _application!.Invoke(() => Dispatch(finished));
        Resume();
        _stalls?.Resume();
    }

    private ExternalEditResult Edit(string text)
    {
        try
        {
            return _editor.EditAsync(text, _shutdownToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return ExternalEditResult.Failed("cancelled");
        }
        catch (Exception exception)
        {
            return ExternalEditResult.Failed($"the editor could not be started ({exception.GetType().Name})");
        }
    }

    // Replays under the lock, so work posted meanwhile queues behind the older work. Ticks were dropped while the
    // editor ran; one fresh tick lets the reducer decide about refreshes.
    private void Resume()
    {
        lock (_invokeLock)
        {
            foreach (var work in _held.Release())
            {
                _application!.Invoke(work);
            }
        }

        PostTick(null);
    }
}
