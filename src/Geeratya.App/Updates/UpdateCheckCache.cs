using System.Text.Json;

namespace Geeratya.App.Updates;

/// <summary>
/// Remembers when GitHub was last asked and what it answered (<c>update-check.json</c> in the state folder), so the
/// background check goes out at most once a day. A missing or damaged file means "never asked".
/// </summary>
public sealed class UpdateCheckCache
{
    /// <summary>How long an answer is reused before GitHub is asked again.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private const string FileName = "update-check.json";

    private readonly string _path;
    private readonly TimeProvider _time;

    public UpdateCheckCache(string stateDirectory, TimeProvider? time = null)
    {
        _path = Path.Combine(stateDirectory, FileName);
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The remembered latest version when it is younger than <see cref="MaxAge"/>, otherwise null.</summary>
    public string? FreshLatest()
    {
        var record = Read();

        if (record?.Latest is null)
        {
            return null;
        }

        var age = _time.GetUtcNow() - record.CheckedAt;

        return age >= TimeSpan.Zero && age < MaxAge ? record.Latest : null;
    }

    /// <summary>Remembers an answer; a file that cannot be written only means the next start asks again.</summary>
    public void Save(string latest)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var record = new UpdateCheckRecord { CheckedAt = _time.GetUtcNow(), Latest = latest };

            File.WriteAllText(_path, JsonSerializer.Serialize(record, UpdateJsonContext.Default.UpdateCheckRecord));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The state folder is not writable: no cache, one request a start.
        }
    }

    private UpdateCheckRecord? Read()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), UpdateJsonContext.Default.UpdateCheckRecord);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
