using Geeratya.Core.Storage;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>
/// Simulates a target held by a scanner or another instance (a Windows sharing violation): the first replaces of
/// matching targets throw, later ones really move the file. Records the pauses instead of sleeping.
/// </summary>
public sealed class LockedFileReplacer : IFileReplacer
{
    private readonly Func<string, bool> _isLocked;
    private readonly int _failures;
    private readonly Exception _error;
    private int _failed;

    public LockedFileReplacer(Func<string, bool> isLocked, int failures, Exception? error = null)
    {
        _isLocked = isLocked;
        _failures = failures;
        _error = error ?? new IOException("The process cannot access the file because it is being used.");
    }

    public List<TimeSpan> Pauses { get; } = [];

    public List<string> Temporaries { get; } = [];

    public int Attempts { get; private set; }

    public void Replace(string temporary, string target)
    {
        Attempts++;
        Temporaries.Add(temporary);

        if (_isLocked(target) && _failed < _failures)
        {
            _failed++;

            throw _error;
        }

        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
        Pauses.Add(duration);
    }
}
