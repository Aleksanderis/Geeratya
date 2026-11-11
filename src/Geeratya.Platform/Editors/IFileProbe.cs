namespace Geeratya.Platform.Editors;

/// <summary>Asks whether a file exists; a seam so program lookup is tested without a real file system.</summary>
public interface IFileProbe
{
    bool FileExists(string path);
}
