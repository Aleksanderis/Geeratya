using Geeratya.Platform.Editors;

namespace Geeratya.Platform.Tests.Editors;

/// <summary>A file system that holds exactly the given paths, compared case-insensitively like Windows.</summary>
public sealed class FakeFileProbe : IFileProbe
{
    private readonly HashSet<string> _files;

    public FakeFileProbe(params string[] files)
    {
        _files = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
    }

    public bool FileExists(string path)
    {
        return _files.Contains(path);
    }
}
