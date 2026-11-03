namespace Geeratya.Core.Cache;

/// <summary>Creates folders as the process default does and restricts nothing.</summary>
public sealed class PlainFilePermissions : IFilePermissions
{
    public static PlainFilePermissions Instance { get; } = new();

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void RestrictFile(string path)
    {
    }
}
