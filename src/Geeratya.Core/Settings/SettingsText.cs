using System.Text;
using System.Text.Json;

namespace Geeratya.Core.Settings;

/// <summary>
/// The minimal text edit of <c>settings.json</c>: only the value of the changed top-level key is replaced (or the key
/// is added as the last member), so comments, key order, indentation and trailing commas stay as the user wrote them.
/// A file that does not parse is never edited.
/// </summary>
public static class SettingsText
{
    public const int MaxBytes = 1_000_000;

    private const string DefaultIndent = "  ";

    private static readonly JsonReaderOptions _readerOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Sets <paramref name="key"/> to the JSON text <paramref name="rawValue"/>.</summary>
    public static SettingsEdit Set(string text, string key, string rawValue)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        if (bytes.Length > MaxBytes)
        {
            return new SettingsEdit(null, "settings.json is larger than 1 MB and is not edited.");
        }

        try
        {
            var layout = Scan(bytes, key);

            return layout is null
                ? new SettingsEdit(null, "settings.json is not a JSON object, so it is not changed.")
                : new SettingsEdit(Apply(text, bytes, layout, key, rawValue), null);
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is { } number ? $" (line {number + 1})" : string.Empty;

            return new SettingsEdit(null, $"settings.json is not valid JSON{line}, so it is not changed.");
        }
    }

    private static ObjectLayout? Scan(byte[] bytes, string key)
    {
        var reader = new Utf8JsonReader(bytes, _readerOptions);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        var layout = new ObjectLayout { OpenIndex = (int)reader.TokenStartIndex };

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var start = (int)reader.TokenStartIndex;
            var matches = reader.ValueTextEquals(key);

            reader.Read();
            ReadValue(ref reader, layout, start, matches);
        }

        layout.CloseIndex = (int)reader.TokenStartIndex;

        if (reader.Read())
        {
            throw new JsonException("Content after the object.");
        }

        return layout;
    }

    private static void ReadValue(ref Utf8JsonReader reader, ObjectLayout layout, int memberStart, bool matches)
    {
        var valueStart = (int)reader.TokenStartIndex;

        reader.Skip();

        var valueEnd = reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray
            ? (int)reader.TokenStartIndex + 1
            : (int)reader.BytesConsumed;

        if (layout.FirstMemberStart < 0)
        {
            layout.FirstMemberStart = memberStart;
        }

        layout.LastValueEnd = valueEnd;

        if (matches)
        {
            layout.ValueStart = valueStart;
            layout.ValueEnd = valueEnd;
        }
    }

    private static string Apply(string text, byte[] bytes, ObjectLayout layout, string key, string rawValue)
    {
        if (layout.ValueStart >= 0)
        {
            return Replace(bytes, layout.ValueStart, layout.ValueEnd, rawValue);
        }

        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var member = $"{SettingsJson.Quote(key)}: {rawValue}";

        if (layout.LastValueEnd >= 0)
        {
            var indent = IndentOf(bytes, layout.FirstMemberStart);

            return Replace(bytes, layout.LastValueEnd, layout.LastValueEnd, $",{newline}{indent}{member}");
        }

        var inner = bytes.AsSpan(layout.OpenIndex + 1, layout.CloseIndex - layout.OpenIndex - 1);
        var onlySpaces = Encoding.UTF8.GetString(inner).All(char.IsWhiteSpace);
        var insertion = $"{newline}{DefaultIndent}{member}{newline}";

        return onlySpaces
            ? Replace(bytes, layout.OpenIndex + 1, layout.CloseIndex, insertion)
            : Replace(bytes, layout.OpenIndex + 1, layout.OpenIndex + 1, $"{newline}{DefaultIndent}{member}");
    }

    private static string Replace(byte[] bytes, int start, int end, string replacement)
    {
        var before = Encoding.UTF8.GetString(bytes, 0, start);
        var after = Encoding.UTF8.GetString(bytes, end, bytes.Length - end);

        return $"{before}{replacement}{after}";
    }

    /// <summary>The whitespace in front of the member that starts at <paramref name="memberStart"/>.</summary>
    private static string IndentOf(byte[] bytes, int memberStart)
    {
        var lineStart = memberStart;

        while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        var indent = Encoding.UTF8.GetString(bytes, lineStart, memberStart - lineStart);

        return indent.All(character => character is ' ' or '\t') ? indent : DefaultIndent;
    }

    private sealed class ObjectLayout
    {
        public int OpenIndex { get; init; }

        public int CloseIndex { get; set; }

        public int FirstMemberStart { get; set; } = -1;

        public int LastValueEnd { get; set; } = -1;

        public int ValueStart { get; set; } = -1;

        public int ValueEnd { get; set; } = -1;
    }
}
