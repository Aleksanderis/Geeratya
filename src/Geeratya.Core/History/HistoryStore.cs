using System.Text.Json;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Storage;

namespace Geeratya.Core.History;

/// <summary>
/// The history files of all profiles below one root (the platform state directory), one folder per profile named
/// like its cache folder (a hash, so no site or e-mail appears in a path). Every call reads and writes under one
/// lock, so concurrent updates never lose an entry. A missing or unreadable file is an empty history.
/// </summary>
public sealed class HistoryStore
{
    public const string FileName = "history.json";

    private readonly string _root;
    private readonly IFilePermissions _permissions;
    private readonly AtomicFile _file;
    private readonly object _lock = new();

    public HistoryStore(string root, IFilePermissions? permissions = null, AtomicFile? file = null)
    {
        _root = root;
        _permissions = permissions ?? PlainFilePermissions.Instance;
        _file = file ?? new AtomicFile();
    }

    /// <summary>The folder of a profile's history (it may not exist yet).</summary>
    public string FolderOf(Profile profile)
    {
        return Path.Combine(_root, ProfileFolder.NameOf(profile));
    }

    public SiteHistory Load(Profile profile)
    {
        lock (_lock)
        {
            return Read(PathOf(profile));
        }
    }

    /// <summary>Applies <paramref name="change"/> to the stored history, saves the result and returns it.</summary>
    public SiteHistory Update(Profile profile, Func<SiteHistory, SiteHistory> change)
    {
        lock (_lock)
        {
            var path = PathOf(profile);
            var updated = change(Read(path));

            Write(path, updated);

            return updated;
        }
    }

    /// <summary>Deletes the profile's history file and its folder when that is left empty.</summary>
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

            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    private static SiteHistory Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return SiteHistory.Empty;
            }

            var history = JsonSerializer.Deserialize(AtomicFile.ReadAllText(path), HistoryJsonContext.Default.SiteHistory);

            return history?.Normalized() ?? SiteHistory.Empty;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return SiteHistory.Empty;
        }
    }

    private string PathOf(Profile profile)
    {
        return Path.Combine(FolderOf(profile), FileName);
    }

    private void Write(string path, SiteHistory history)
    {
        var folder = Path.GetDirectoryName(path)!;

        _permissions.CreateDirectory(folder);
        _file.WriteAllText(
            path,
            JsonSerializer.Serialize(history, HistoryJsonContext.Default.SiteHistory),
            _permissions.RestrictFile);
    }
}
