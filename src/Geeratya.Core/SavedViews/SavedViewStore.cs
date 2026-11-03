using System.Text.Json;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;

namespace Geeratya.Core.SavedViews;

/// <summary>
/// The saved-view files of all profiles below one root (the platform state directory), one file per profile in the
/// profile's hashed folder, written through a temporary file and a move with user-only permissions. Every call reads
/// and writes under one lock. A missing file or one that cannot be read is an empty list on load; an update never
/// writes over a file it could not read (the I/O error propagates), and a corrupt file is moved to
/// <c>saved-views.json.bad</c> before the next save replaces it.
/// </summary>
public sealed class SavedViewStore
{
    public const string FileName = "saved-views.json";

    private const string TemporarySuffix = ".tmp";
    private const string BadSuffix = ".bad";

    private readonly string _root;
    private readonly FilePermissions _permissions;
    private readonly object _lock = new();

    public SavedViewStore(string root, FilePermissions? permissions = null)
    {
        _root = root;
        _permissions = permissions ?? FilePermissions.Plain;
    }

    /// <summary>The folder of a profile's file (it may not exist yet).</summary>
    public string FolderOf(Profile profile)
    {
        return Path.Combine(_root, ProfileFolder.NameOf(profile));
    }

    public SavedViewList Load(Profile profile)
    {
        lock (_lock)
        {
            try
            {
                return Read(PathOf(profile));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return SavedViewList.Empty;
            }
        }
    }

    /// <summary>Applies <paramref name="change"/> to the stored views, saves the result and returns it.</summary>
    public SavedViewList Update(Profile profile, Func<SavedViewList, SavedViewList> change)
    {
        lock (_lock)
        {
            var path = PathOf(profile);
            var updated = change(Read(path));

            Write(path, updated);

            return updated;
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

            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    private static SavedViewList Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return SavedViewList.Empty;
            }

            var views = JsonSerializer.Deserialize(File.ReadAllText(path), SavedViewsJsonContext.Default.SavedViewList);

            return views?.Normalized() ?? SavedViewList.Empty;
        }
        catch (JsonException)
        {
            File.Move(path, $"{path}{BadSuffix}", overwrite: true);

            return SavedViewList.Empty;
        }
    }

    private string PathOf(Profile profile)
    {
        return Path.Combine(FolderOf(profile), FileName);
    }

    private void Write(string path, SavedViewList views)
    {
        var folder = Path.GetDirectoryName(path)!;
        var temporary = $"{path}{TemporarySuffix}";

        _permissions.CreateDirectory(folder);

        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(views, SavedViewsJsonContext.Default.SavedViewList));
            _permissions.RestrictFile(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Delete(temporary);

            throw;
        }
    }
}
