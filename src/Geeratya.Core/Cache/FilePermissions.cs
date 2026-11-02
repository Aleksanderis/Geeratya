namespace Geeratya.Core.Cache;

/// <summary>
/// How the cache makes its folders and files readable by the user only. The platform layer supplies the real
/// implementation (Core never references it); <see cref="Plain"/> applies no restriction and serves tests.
/// </summary>
/// <param name="CreateDirectory">Creates the folder (and parents) with user-only access.</param>
/// <param name="RestrictFile">Limits an existing file to the user.</param>
public sealed record FilePermissions(Action<string> CreateDirectory, Action<string> RestrictFile)
{
    public static FilePermissions Plain { get; } = new(path => Directory.CreateDirectory(path), static _ => { });
}
