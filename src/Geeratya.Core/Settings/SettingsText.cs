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

    /// <summary>Sets the top-level <paramref name="key"/> to the JSON text <paramref name="rawValue"/>.</summary>
    public static SettingsEdit Set(string text, string key, string rawValue)
    {
        return Set(text, [key], rawValue);
    }

    /// <summary>
    /// Sets the member at <paramref name="path"/> (<c>["hints", "statusBar"]</c> is <c>hints.statusBar</c>) to the JSON
    /// text <paramref name="rawValue"/>. A missing parent object is created; a parent that is not an object refuses.
    /// </summary>
    public static SettingsEdit Set(string text, IReadOnlyList<string> path, string rawValue)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        if (bytes.Length > MaxBytes)
        {
            return new SettingsEdit(null, "settings.json is larger than 1 MB and is not edited.");
        }

        try
        {
            return Edit(text, bytes, path, rawValue);
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is { } number ? $" (line {number + 1})" : string.Empty;

            return new SettingsEdit(null, $"settings.json is not valid JSON{line}, so it is not changed.");
        }
    }

    private static SettingsEdit Edit(string text, byte[] bytes, IReadOnlyList<string> path, string rawValue)
    {
        var start = 0;
        var length = bytes.Length;

        for (var depth = 0; depth < path.Count; depth++)
        {
            var key = path[depth];
            var layout = Scan(bytes, start, length, key);

            if (layout is null)
            {
                var name = depth == 0 ? "settings.json is" : $"\"{path[depth - 1]}\" in settings.json is";

                return new SettingsEdit(null, $"{name} not a JSON object, so it is not changed.");
            }

            var last = depth == path.Count - 1;

            if (layout.ValueStart < 0)
            {
                return new SettingsEdit(Apply(text, bytes, layout, key, Nest(path.Skip(depth + 1), rawValue)), null);
            }

            if (last)
            {
                return new SettingsEdit(Replace(bytes, layout.ValueStart, layout.ValueEnd, rawValue), null);
            }

            start = layout.ValueStart;
            length = layout.ValueEnd - layout.ValueStart;
        }

        return new SettingsEdit(null, "No setting key given.");
    }

    /// <summary>The value wrapped in one inline object per remaining path segment.</summary>
    private static string Nest(IEnumerable<string> rest, string rawValue)
    {
        return rest.Reverse().Aggregate(rawValue, (inner, key) => $"{{ {SettingsJson.Quote(key)}: {inner} }}");
    }

    /// <summary>
    /// Scans the JSON value at <paramref name="start"/>; null when it is not an object. Offsets in the layout are in
    /// <paramref name="bytes"/>, not in the slice.
    /// </summary>
    private static ObjectLayout? Scan(byte[] bytes, int start, int length, string key)
    {
        var reader = new Utf8JsonReader(bytes.AsSpan(start, length), _readerOptions);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        var layout = new ObjectLayout { OpenIndex = start + (int)reader.TokenStartIndex };

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var memberStart = start + (int)reader.TokenStartIndex;
            var matches = reader.ValueTextEquals(key);

            reader.Read();
            ReadValue(ref reader, layout, start, memberStart, matches);
        }

        layout.CloseIndex = start + (int)reader.TokenStartIndex;

        if (reader.Read())
        {
            throw new JsonException("Content after the object.");
        }

        return layout;
    }

    private static void ReadValue(
        ref Utf8JsonReader reader,
        ObjectLayout layout,
        int offset,
        int memberStart,
        bool matches)
    {
        var valueStart = offset + (int)reader.TokenStartIndex;

        reader.Skip();

        var closes = reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray;
        var relativeEnd = closes ? (int)reader.TokenStartIndex + 1 : (int)reader.BytesConsumed;
        var valueEnd = offset + relativeEnd;

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
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var member = $"{SettingsJson.Quote(key)}: {rawValue}";

        if (layout.LastValueEnd >= 0)
        {
            var indent = IndentOf(bytes, layout.FirstMemberStart);

            return Replace(bytes, layout.LastValueEnd, layout.LastValueEnd, $",{newline}{indent}{member}");
        }

        var outer = LineIndent(bytes, layout.OpenIndex);
        var inner = bytes.AsSpan(layout.OpenIndex + 1, layout.CloseIndex - layout.OpenIndex - 1);
        var onlySpaces = Encoding.UTF8.GetString(inner).All(char.IsWhiteSpace);
        var insertion = $"{newline}{outer}{DefaultIndent}{member}{newline}{outer}";

        return onlySpaces
            ? Replace(bytes, layout.OpenIndex + 1, layout.CloseIndex, insertion)
            : Replace(bytes, layout.OpenIndex + 1, layout.OpenIndex + 1, $"{newline}{outer}{DefaultIndent}{member}");
    }

    /// <summary>The leading whitespace of the line that holds <paramref name="index"/>.</summary>
    private static string LineIndent(byte[] bytes, int index)
    {
        var lineStart = index;

        while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        var end = lineStart;

        while (end < index && bytes[end] is (byte)' ' or (byte)'\t')
        {
            end++;
        }

        return Encoding.UTF8.GetString(bytes, lineStart, end - lineStart);
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
