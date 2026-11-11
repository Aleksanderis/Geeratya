namespace Geeratya.Platform.Editors;

/// <summary>The real file system.</summary>
public sealed class FileProbe : IFileProbe
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }
}
