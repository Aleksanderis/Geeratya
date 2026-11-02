using System.Security.Cryptography;
using System.Text;
using Geeratya.Core.Profiles;

namespace Geeratya.Core.Cache;

/// <summary>
/// The caches of all profiles below one root (the platform cache directory), one folder per profile named by a
/// hash of the profile's credential key, so no site, e-mail or token appears in a path. The host asks for the
/// cache of the active profile; a switch only drops the memory part, removing a profile deletes its folder.
/// </summary>
public sealed class SiteCaches : IDisposable
{
    private const int FolderHashLength = 16;

    private readonly string _root;
    private readonly FilePermissions _permissions;
    private readonly TimeSpan? _debounce;
    private readonly long _maxDiskBytes;
    private readonly object _lock = new();
    private readonly Dictionary<string, SiteCache> _open = [];

    public SiteCaches(
        string root,
        FilePermissions? permissions = null,
        TimeSpan? debounce = null,
        long maxDiskBytes = SiteCache.DefaultMaxDiskBytes)
    {
        _root = root;
        _permissions = permissions ?? FilePermissions.Plain;
        _debounce = debounce;
        _maxDiskBytes = maxDiskBytes;
    }

    /// <summary>Raised with a one-line notice when a profile's cache could not be written.</summary>
    public event Action<string>? WriteFailed;

    /// <summary>The folder of a profile's cache (it may not exist yet).</summary>
    public string FolderOf(Profile profile)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(profile.CredentialKey));

        return Path.Combine(_root, Convert.ToHexStringLower(hash)[..FolderHashLength]);
    }

    /// <summary>The open cache of <paramref name="profile"/>, created on first use (no file is touched yet).</summary>
    public SiteCache For(Profile profile)
    {
        var folder = FolderOf(profile);

        lock (_lock)
        {
            if (_open.TryGetValue(folder, out var existing))
            {
                return existing;
            }

            var created = new SiteCache(folder, _permissions, _debounce, _maxDiskBytes);

            created.WriteFailed += OnWriteFailed;
            _open[folder] = created;

            return created;
        }
    }

    /// <summary>Drops the memory part and pending writes of a profile; its folder stays.</summary>
    public void Drop(Profile profile)
    {
        SiteCache? cache;

        lock (_lock)
        {
            _open.Remove(FolderOf(profile), out cache);
        }

        cache?.Discard();
    }

    /// <summary>
    /// Removes the profile's cache: first closes it (so no pending write or exit flush can recreate the folder),
    /// then deletes the folder. Other profiles' folders are never touched. False when files could not be deleted.
    /// </summary>
    public bool Delete(Profile profile)
    {
        var folder = FolderOf(profile);

        Close(folder);

        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Writes every open cache's pending views (the application is ending).</summary>
    public void FlushAll()
    {
        SiteCache[] caches;

        lock (_lock)
        {
            caches = [.. _open.Values];
        }

        foreach (var cache in caches)
        {
            cache.Flush();
        }
    }

    public void Dispose()
    {
        SiteCache[] caches;

        lock (_lock)
        {
            caches = [.. _open.Values];
            _open.Clear();
        }

        foreach (var cache in caches)
        {
            cache.Dispose();
        }
    }

    private void Close(string folder)
    {
        SiteCache? cache;

        lock (_lock)
        {
            _open.Remove(folder, out cache);
        }

        cache?.Dispose();
    }

    private void OnWriteFailed(string notice)
    {
        WriteFailed?.Invoke(notice);
    }
}
