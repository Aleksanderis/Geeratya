using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Geeratya.Core.Settings;

/// <summary>
/// Reads and writes <c>settings.json</c> in the configuration directory. The file is JSON with comments and trailing
/// commas; a write replaces only the changed value (<see cref="SettingsText"/>) and keeps everything else.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private const string TemporarySuffix = ".tmp";

    private const string EmptyObject = "{\n}\n";

    private static readonly byte[] _byteOrderMark = Encoding.UTF8.GetPreamble();

    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _path;
    private readonly Lock _gate = new();

    private bool _baselineKnown;
    private string? _baseline;

    public SettingsStore(string configurationDirectory)
    {
        _path = Path.Combine(configurationDirectory, FileName);
    }

    /// <summary>The full path of the file (it may not exist yet).</summary>
    public string FilePath => _path;

    /// <summary>The saved settings; see <see cref="Read"/> for the warnings.</summary>
    public AppSettings Load()
    {
        return Read().Settings;
    }

    /// <summary>
    /// The saved settings and a warning per problem: the defaults when the file is missing or does not parse (the file
    /// is left as it is), and the default for each value that is invalid. File system errors still throw.
    /// </summary>
    public SettingsLoad Read()
    {
        lock (_gate)
        {
            var bytes = ReadBytes();

            RememberBaseline(bytes);

            return bytes is null ? new SettingsLoad(AppSettings.Default, []) : Parse(bytes);
        }
    }

    /// <summary>
    /// Writes one setting. Refused when the file does not parse; when it changed since it was read, it is re-read and
    /// nothing is written, so the caller can ask for the change to be repeated.
    /// </summary>
    public SettingsWrite Write(SettingDefinition setting, AppSettings settings)
    {
        lock (_gate)
        {
            var target = ResolveTarget();
            var bytes = ReadBytes();

            if (_baselineKnown && Hash(bytes) != _baseline)
            {
                RememberBaseline(bytes);

                return Stale(bytes);
            }

            var edit = SettingsText.Set(
                CurrentText(bytes),
                setting.ConfigPath,
                SettingsRegistry.ToJson(setting, settings));

            if (edit.Text is not { } text)
            {
                return new SettingsWrite(SettingsWriteOutcome.Refused, edit.Error, null);
            }

            var written = Encode(bytes, text);

            WriteThroughTemporaryFile(target, written);
            RememberBaseline(written);

            return new SettingsWrite(SettingsWriteOutcome.Saved, null, null);
        }
    }

    private static SettingsWrite Stale(byte[]? bytes)
    {
        var reloaded = bytes is null ? new SettingsLoad(AppSettings.Default, []) : Parse(bytes);

        return new SettingsWrite(
            SettingsWriteOutcome.Stale,
            "settings.json changed on disk; it was read again. Repeat the change.",
            reloaded.Settings);
    }

    private byte[]? ReadBytes()
    {
        return File.Exists(_path) ? File.ReadAllBytes(_path) : null;
    }

    private void RememberBaseline(byte[]? bytes)
    {
        _baselineKnown = true;
        _baseline = Hash(bytes);
    }

    private static string? Hash(byte[]? bytes)
    {
        return bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string CurrentText(byte[]? bytes)
    {
        var text = bytes is null ? string.Empty : Decode(bytes);

        return string.IsNullOrWhiteSpace(text) ? EmptyObject : text;
    }

    private static string Decode(byte[] bytes)
    {
        var skip = bytes.AsSpan().StartsWith(_byteOrderMark) ? _byteOrderMark.Length : 0;

        return Encoding.UTF8.GetString(bytes, skip, bytes.Length - skip);
    }

    /// <summary>The bytes to write, keeping the byte order mark the file had.</summary>
    private static byte[] Encode(byte[]? original, string text)
    {
        var body = Encoding.UTF8.GetBytes(text);

        return original is not null && original.AsSpan().StartsWith(_byteOrderMark) ? [.. _byteOrderMark, .. body] : body;
    }

    private static SettingsLoad Parse(byte[] bytes)
    {
        var text = Decode(bytes);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new SettingsLoad(AppSettings.Default, []);
        }

        try
        {
            using var document = JsonDocument.Parse(text, _documentOptions);

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? ReadMembers(document.RootElement)
                : Unusable("is not a JSON object");
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is { } number ? $" (line {number + 1})" : string.Empty;

            return Unusable($"is not valid JSON{line}");
        }
    }

    private static SettingsLoad Unusable(string reason)
    {
        return new SettingsLoad(AppSettings.Default, [$"settings.json {reason}; defaults are used."]);
    }

    private static SettingsLoad ReadMembers(JsonElement root)
    {
        var settings = AppSettings.Default;
        List<string> warnings = [];
        HashSet<string> reported = [];

        foreach (var setting in SettingsRegistry.All)
        {
            if (BadParent(root, setting) is { } parent && !HasLegacy(root, setting) && reported.Add(parent))
            {
                warnings.Add($"settings.json: {parent} must be an object; its settings use their defaults.");
            }

            var (value, key) = Lookup(root, setting);

            if (value is not { } found)
            {
                continue;
            }

            var applied = Apply(setting, settings, found);

            if (applied is null)
            {
                warnings.Add(
                    $"settings.json: {key} must be {setting.Expected}; using {setting.Default}.");
            }
            else
            {
                settings = applied;
            }
        }

        return new SettingsLoad(settings, warnings);
    }

    private static bool HasLegacy(JsonElement root, SettingDefinition setting)
    {
        return setting.LegacyConfigKey is { } legacy && TryGetLast(root, legacy, out _);
    }

    /// <summary>The dotted name of the first group on the setting's path that exists but is not an object, else null.</summary>
    private static string? BadParent(JsonElement root, SettingDefinition setting)
    {
        var current = root;
        var path = setting.ConfigPath;

        for (var index = 0; index < path.Length - 1; index++)
        {
            if (!TryGetLast(current, path[index], out current))
            {
                return null;
            }

            if (current.ValueKind != JsonValueKind.Object)
            {
                return string.Join('.', path.Take(index + 1));
            }
        }

        return null;
    }

    /// <summary>
    /// The value under the new nested path, else under the older flat key, with the key it was found at.
    /// </summary>
    private static SettingLookup Lookup(JsonElement root, SettingDefinition setting)
    {
        var current = root;

        foreach (var segment in setting.ConfigPath)
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetLast(current, segment, out current))
            {
                return LookupLegacy(root, setting);
            }
        }

        return new SettingLookup(current, setting.ConfigKey);
    }

    private static SettingLookup LookupLegacy(JsonElement root, SettingDefinition setting)
    {
        if (setting.LegacyConfigKey is { } legacy && TryGetLast(root, legacy, out var old))
        {
            return new SettingLookup(old, legacy);
        }

        return new SettingLookup(null, setting.ConfigKey);
    }

    private static bool TryGetLast(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        var found = false;

        foreach (var member in element.EnumerateObject().Where(member => member.NameEquals(name)))
        {
            value = member.Value;
            found = true;
        }

        return found;
    }

    private static AppSettings? Apply(SettingDefinition setting, AppSettings settings, JsonElement value)
    {
        var text = (setting.Kind, value.ValueKind) switch
        {
            (SettingKind.Toggle, JsonValueKind.True) => SettingsRegistry.On,
            (SettingKind.Toggle, JsonValueKind.False) => SettingsRegistry.Off,
            (SettingKind.Choice, JsonValueKind.True) => SettingsRegistry.On,
            (SettingKind.Choice, JsonValueKind.False) => SettingsRegistry.Off,
            (SettingKind.Choice, JsonValueKind.String) => value.GetString(),
            (SettingKind.Text, JsonValueKind.String) => value.GetString(),
            _ => null,
        };

        return text is null ? null : setting.Apply(settings, text);
    }

    /// <summary>The file a symbolic link points to, so the link survives the rename.</summary>
    private string ResolveTarget()
    {
        if (!File.Exists(_path))
        {
            return _path;
        }

        return File.ResolveLinkTarget(_path, returnFinalTarget: true)?.FullName ?? _path;
    }

    private static void WriteThroughTemporaryFile(string target, byte[] content)
    {
        var directory = Path.GetDirectoryName(target);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{target}{TemporarySuffix}";

        try
        {
            File.WriteAllBytes(temporary, content);
            KeepPermissions(target, temporary);
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);

            throw;
        }
    }

    /// <summary>Best effort: the new file gets the mode of the one it replaces (Unix only).</summary>
    private static void KeepPermissions(string target, string temporary)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(target))
        {
            return;
        }

        File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
    }
}
