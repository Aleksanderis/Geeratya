using System.Globalization;

namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Appends stalls to <c>stalls.log</c> in the state folder, one line each (time, length, last work): the only trace of
/// a frozen UI thread that can be written while it is frozen. The file is dropped when it grows past 64 KB, and a
/// write that fails is ignored.
/// </summary>
public sealed class FileStallLog : IStallReport
{
    public const string FileName = "stalls.log";

    private const long MaxBytes = 64 * 1024;

    private readonly string _path;
    private readonly TimeProvider _clock;

    public FileStallLog(string stateDirectory, TimeProvider clock)
    {
        _path = Path.Combine(stateDirectory, FileName);
        _clock = clock;
    }

    public void Stalled(TimeSpan stalled, string lastWork)
    {
        Append($"UI thread stalled for {Seconds(stalled)} s; last work started: {lastWork}");
    }

    public void Recovered(TimeSpan stalled)
    {
        Append($"UI thread running again after about {Seconds(stalled)} s");
    }

    private static string Seconds(TimeSpan span)
    {
        return span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void Append(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
            {
                File.Delete(_path);
            }

            File.AppendAllText(_path, $"{_clock.GetUtcNow():O} {text}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics only: never a reason to fail.
        }
    }
}
