using System.Text;

namespace Geeratya.Tui;

/// <summary>
/// The one way raw terminal output (such as the OSC 52 clipboard sequence) leaves the program besides the frame.
/// Writes run on the UI thread, the thread that draws frames, so a sequence never lands inside a frame.
/// </summary>
public sealed class TerminalWriter : TextWriter
{
    private readonly TextWriter _console;
    private readonly IUiThread _uiThread;

    public TerminalWriter(TextWriter console, IUiThread uiThread)
    {
        _console = console;
        _uiThread = uiThread;
    }

    public override Encoding Encoding => _console.Encoding;

    public override void Write(char value)
    {
        Write(value.ToString());
    }

    public override void Write(string? value)
    {
        _uiThread.InvokeAsync(() => _console.Write(value), CancellationToken.None).GetAwaiter().GetResult();
    }

    public override void Flush()
    {
        _uiThread.InvokeAsync(_console.Flush, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        var text = buffer.ToString();

        return _uiThread.InvokeAsync(() => _console.Write(text), cancellationToken);
    }

    public override Task WriteAsync(string? value)
    {
        return _uiThread.InvokeAsync(() => _console.Write(value), CancellationToken.None);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _uiThread.InvokeAsync(_console.Flush, cancellationToken);
    }
}
