namespace Geeratya.Core.Cache;

/// <summary>
/// How the cache makes its folders and files readable by the user only. The platform layer supplies the real
/// implementation (Core never references it); <see cref="PlainFilePermissions"/> applies no restriction and serves
/// tests.
/// </summary>
public interface IFilePermissions
{
    /// <summary>Creates the folder (and parents) with user-only access.</summary>
    void CreateDirectory(string path);

    /// <summary>Limits an existing file to the user.</summary>
    void RestrictFile(string path);
}
