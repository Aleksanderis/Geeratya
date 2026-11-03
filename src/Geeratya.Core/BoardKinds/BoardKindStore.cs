using System.Globalization;
using System.Text.Json;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.BoardKinds;

/// <summary>
/// The Scrum/Kanban answers of the probe, per profile (roadmap open question 4): one <c>board-kinds.json</c> in the
/// profile's hashed state folder (the folder of the history, user-only permissions, written through a temp file).
/// Only board ids and the words scrum/kanban are stored. A missing or unreadable file is an empty answer set.
/// </summary>
public sealed class BoardKindStore
{
    public const string FileName = "board-kinds.json";

    private const string TemporarySuffix = ".tmp";
    private const string ScrumName = "scrum";
    private const string KanbanName = "kanban";

    private readonly string _root;
    private readonly FilePermissions _permissions;
    private readonly object _lock = new();
    private readonly HashSet<string> _removed = [];

    public BoardKindStore(string root, FilePermissions? permissions = null)
    {
        _root = root;
        _permissions = permissions ?? FilePermissions.Plain;
    }

    public IReadOnlyDictionary<long, BoardKind> Load(Profile profile)
    {
        lock (_lock)
        {
            // Loading is the connection being (re)built, so a re-added profile stores answers again (a probe of the
            // old connection still in flight then stores an answer for the same site, which is harmless).
            _removed.Remove(ProfileFolder.NameOf(profile));

            return Parse(Read(PathOf(profile)));
        }
    }

    public void Save(Profile profile, long boardId, BoardKind kind)
    {
        lock (_lock)
        {
            if (_removed.Contains(ProfileFolder.NameOf(profile)))
            {
                // A late probe reply of a removed profile must not bring its file back.
                return;
            }

            var path = PathOf(profile);
            var file = Read(path);

            file.Boards[boardId.ToString(CultureInfo.InvariantCulture)] = kind == BoardKind.Scrum ? ScrumName : KanbanName;
            Write(path, file);
        }
    }

    /// <summary>
    /// Deletes the profile's file (and a stray temp file) and its folder when that is left empty. Later saves for the
    /// profile are ignored until it is loaded again.
    /// </summary>
    public void Delete(Profile profile)
    {
        lock (_lock)
        {
            _removed.Add(ProfileFolder.NameOf(profile));

            var folder = Path.Combine(_root, ProfileFolder.NameOf(profile));

            if (!Directory.Exists(folder))
            {
                return;
            }

            File.Delete(PathOf(profile));
            File.Delete($"{PathOf(profile)}{TemporarySuffix}");

            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    private static Dictionary<long, BoardKind> Parse(BoardKindFile file)
    {
        var result = new Dictionary<long, BoardKind>();

        foreach (var (key, value) in file.Boards)
        {
            if (!long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            switch (value)
            {
                case ScrumName:
                    result[id] = BoardKind.Scrum;

                    break;
                case KanbanName:
                    result[id] = BoardKind.Kanban;

                    break;
            }
        }

        return result;
    }

    private static BoardKindFile Read(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var file = JsonSerializer.Deserialize(File.ReadAllText(path), BoardKindJsonContext.Default.BoardKindFile);

                if (file?.Boards is not null)
                {
                    return file;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A store of answers: a bad file is the same as none, the probe asks again.
        }

        return new BoardKindFile([]);
    }

    private string PathOf(Profile profile)
    {
        return Path.Combine(_root, ProfileFolder.NameOf(profile), FileName);
    }

    private void Write(string path, BoardKindFile file)
    {
        var folder = Path.GetDirectoryName(path)!;
        var temporary = $"{path}{TemporarySuffix}";

        _permissions.CreateDirectory(folder);

        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(file, BoardKindJsonContext.Default.BoardKindFile));
            _permissions.RestrictFile(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);

            throw;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: the write failure is the error worth reporting.
        }
    }
}
