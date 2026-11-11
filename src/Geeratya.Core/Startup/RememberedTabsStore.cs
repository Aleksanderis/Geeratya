using System.Text.Json;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Storage;

namespace Geeratya.Core.Startup;

/// <summary>
/// The remembered-tabs files of all profiles below one root (the platform state directory), one file per profile in
/// the profile's hashed folder, written through a temporary file and a move with user-only permissions. A missing or
/// unreadable file is an empty list on load; a corrupt file is moved to <c>tabs.json.bad</c> and read as empty.
/// Like the saved views, it holds definitions only: no rows, no token, no site.
/// </summary>
public sealed class RememberedTabsStore
{
    public const string FileName = "tabs.json";

    private const string BadSuffix = ".bad";

    private readonly string _root;
    private readonly IFilePermissions _permissions;
    private readonly AtomicFile _file;
    private readonly object _lock = new();

    public RememberedTabsStore(string root, IFilePermissions? permissions = null, AtomicFile? file = null)
    {
        _root = root;
        _permissions = permissions ?? PlainFilePermissions.Instance;
        _file = file ?? new AtomicFile();
    }

    /// <summary>The folder of a profile's file (it may not exist yet).</summary>
    public string FolderOf(Profile profile)
    {
        return Path.Combine(_root, ProfileFolder.NameOf(profile));
    }

    public RememberedTabs Load(Profile profile)
    {
        lock (_lock)
        {
            try
            {
                return Read(PathOf(profile));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return RememberedTabs.Empty;
            }
        }
    }

    /// <summary>Replaces the profile's file; an I/O error propagates.</summary>
    public void Save(Profile profile, RememberedTabs tabs)
    {
        lock (_lock)
        {
            Write(PathOf(profile), tabs);
        }
    }

    /// <summary>Deletes the profile's file and its folder when that is left empty.</summary>
    public void Delete(Profile profile)
    {
        lock (_lock)
        {
            var folder = FolderOf(profile);

            if (!Directory.Exists(folder))
            {
                return;
            }

            File.Delete(PathOf(profile));
            AtomicFile.DeleteStrayTemporaries(PathOf(profile));

            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    private static RememberedTabs Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return RememberedTabs.Empty;
            }

            var tabs = JsonSerializer.Deserialize(AtomicFile.ReadAllText(path), StartupJsonContext.Default.RememberedTabs);

            return tabs?.Items is null ? RememberedTabs.Empty : tabs.Normalized();
        }
        catch (JsonException)
        {
            File.Move(path, $"{path}{BadSuffix}", overwrite: true);

            return RememberedTabs.Empty;
        }
    }

    private string PathOf(Profile profile)
    {
        return Path.Combine(FolderOf(profile), FileName);
    }

    private void Write(string path, RememberedTabs tabs)
    {
        var folder = Path.GetDirectoryName(path)!;

        _permissions.CreateDirectory(folder);
        _file.WriteAllText(
            path,
            JsonSerializer.Serialize(tabs, StartupJsonContext.Default.RememberedTabs),
            _permissions.RestrictFile);
    }
}
