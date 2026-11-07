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
    private readonly Queue<Action> _queuedWork = new();
    private EditExternallyEffect? _pendingEdit;
    private bool _suspended;

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

    /// <summary>Thread-safe: runs the work on the UI thread, or queues it while an outside editor owns the terminal.</summary>
    private void InvokeOnUi(Action work)
    {
        lock (_invokeLock)
        {
            if (_suspended)
            {
                _queuedWork.Enqueue(work);

                return;
            }
        }

        _application!.Invoke(work);
    }

    private void RunExternalEditor(EditExternallyEffect edit)
    {
        _pendingEdit = null;

        lock (_invokeLock)
        {
            _suspended = true;
        }

        _uiThread.Detach();
        _application!.Dispose();

        var result = Edit(edit.Text);

        OpenApplication();

        var finished = new ExternalEditFinished(edit.Tab, edit.Key, edit.CommentId, result.Text, result.Error);

        _application!.Invoke(() => Dispatch(finished));
        Resume();
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

    private void Resume()
    {
        List<Action> work;

        lock (_invokeLock)
        {
            _suspended = false;
            work = [.. _queuedWork];
            _queuedWork.Clear();
        }

        foreach (var item in work)
        {
            _application!.Invoke(item);
        }
    }

    private void AskForExternalEdit(EditExternallyEffect edit)
    {
        _pendingEdit = edit;
        _application!.Invoke(_application.RequestStop);
    }
}
