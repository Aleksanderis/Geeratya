using Geeratya.Core.Cache;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>Records the folders created and the files restricted; creates the folders for real.</summary>
public sealed class RecordingFilePermissions : IFilePermissions, IDisposable
{
    private readonly List<string> _folders = [];
    private readonly List<string> _files = [];

    /// <summary>Set when a file was restricted.</summary>
    public ManualResetEventSlim Restricted { get; } = new();

    public IReadOnlyList<string> Folders => _folders;

    public IReadOnlyList<string> Files => _files;

    public void CreateDirectory(string path)
    {
        _folders.Add(path);
        Directory.CreateDirectory(path);
    }

    public void RestrictFile(string path)
    {
        _files.Add(path);
        Restricted.Set();
    }

    public void Dispose()
    {
        Restricted.Dispose();
    }
}
