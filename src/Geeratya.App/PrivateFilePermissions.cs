using Geeratya.Core.Cache;
using Geeratya.Platform.Files;

namespace Geeratya.App;

/// <summary>Gives the cache and the per-profile stores the user-only folders and files of the platform layer.</summary>
public sealed class PrivateFilePermissions(PrivateFiles files) : IFilePermissions
{
    public void CreateDirectory(string path)
    {
        files.CreateDirectory(path);
    }

    public void RestrictFile(string path)
    {
        files.RestrictFile(path);
    }
}
