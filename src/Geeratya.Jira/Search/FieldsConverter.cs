using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geeratya.Jira.Search;

/// <summary>
/// Reads the <c>fields</c> object of a search row. The fixed list-row fields go through the source-generated
/// metadata; every other field is reduced on the spot to an <see cref="ExtraValue"/> (a number or typed array
/// entries) and the rest is skipped, so no <c>JsonElement</c> (and no <c>JsonDocument</c> buffer) lives per issue.
/// Tolerant like the rest of the wire model: a value of an unexpected shape is skipped, never an error.
/// </summary>
internal sealed class FieldsConverter : JsonConverter<SearchWire.Fields>
{
    private const int NameBufferLength = 128;
    private const string IdProperty = "id";
    private const string NameProperty = "name";
    private const string StateProperty = "state";
    private const string KeyProperty = "key";

    public override SearchWire.Fields Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("The fields of an issue must be an object.");
        }

        var values = new FieldValues();
        Span<char> buffer = stackalloc char[NameBufferLength];

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var length = CopyName(ref reader, buffer);
            var name = length >= 0 ? buffer[..length] : reader.GetString().AsSpan();
            reader.Read();
            ReadProperty(ref reader, name, values);
        }

        return values.ToFields();
    }

    public override void Write(Utf8JsonWriter writer, SearchWire.Fields value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Search fields are only read.");

    private static void ReadProperty(ref Utf8JsonReader reader, scoped ReadOnlySpan<char> name, FieldValues values)
    {
        switch (name)
        {
            case IssueFields.Summary:
                values.Summary = WireObjectReader.ReadText(ref reader);
                break;
            case IssueFields.Status:
                values.Status = WireObjectReader.ReadStatus(ref reader);
                break;
            case IssueFields.IssueType:
                values.IssueType = WireObjectReader.ReadIssueType(ref reader);
                break;
            case IssueFields.Priority:
                values.Priority = WireObjectReader.ReadPriority(ref reader);
                break;
            case IssueFields.Assignee:
                values.Assignee = WireObjectReader.ReadAssignee(ref reader);
                break;
            case IssueFields.Labels:
                values.Labels = WireObjectReader.ReadLabels(ref reader);
                break;
            case IssueFields.Parent:
                values.Parent = WireObjectReader.ReadParent(ref reader);
                break;
            case IssueFields.Updated:
                values.Updated = WireObjectReader.ReadText(ref reader);
                break;
            case IssueFields.Project:
                values.Project = WireObjectReader.ReadProject(ref reader);
                break;
            default:
                values.AddExtra(name, ReadExtra(ref reader));
                break;
        }
    }

    /// <summary>
    /// Copies the property name into the buffer without allocating, so only the names of fields that are kept become
    /// strings (an unknown field that is skipped costs nothing). Returns the length, or -1 when it does not fit.
    /// </summary>
    private static int CopyName(ref Utf8JsonReader reader, scoped Span<char> buffer)
    {
        if (reader.ValueSpan.Length > buffer.Length || reader.ValueIsEscaped)
        {
            return -1;
        }

        return reader.CopyString(buffer);
    }

    private static ExtraValue? ReadExtra(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.TryGetDouble(out var number) ? new ExtraValue(number, null, null) : null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            _ = reader.TrySkip();

            return null;
        }

        List<ExtraSprint>? sprints = null;
        List<ExtraSubtask>? subtasks = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                _ = reader.TrySkip();

                continue;
            }

            ReadEntry(ref reader, ref sprints, ref subtasks);
        }

        return sprints is null && subtasks is null ? null : new ExtraValue(null, sprints, subtasks);
    }

    private static void ReadEntry(
        ref Utf8JsonReader reader,
        ref List<ExtraSprint>? sprints,
        ref List<ExtraSubtask>? subtasks)
    {
        long? id = null;
        string? name = null;
        string? state = null;
        string? key = null;
        ExtraSubtaskFields? fields = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isId = reader.ValueTextEquals(IdProperty);
            var isName = reader.ValueTextEquals(NameProperty);
            var isState = reader.ValueTextEquals(StateProperty);
            var isKey = reader.ValueTextEquals(KeyProperty);
            var isFields = reader.ValueTextEquals(IssueFields.FieldsProperty);
            reader.Read();

            if (isId && reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number))
            {
                id = number;
            }
            else if (isFields && reader.TokenType == JsonTokenType.StartObject)
            {
                fields = WireObjectReader.ReadSubtaskFields(ref reader);
            }
            else if (isName || isState || isKey)
            {
                var text = WireObjectReader.ReadText(ref reader);
                name = isName ? text : name;
                state = isState ? text : state;
                key = isKey ? text : key;
            }
            else
            {
                _ = reader.TrySkip();
            }
        }

        if (id is { } sprintId)
        {
            (sprints ??= []).Add(new ExtraSprint(sprintId, name, state));
        }

        if (key is { Length: > 0 })
        {
            (subtasks ??= []).Add(new ExtraSubtask(key, fields));
        }
    }

    /// <summary>The values of one row while its properties are read, in any order.</summary>
    private sealed class FieldValues
    {
        public string? Summary { get; set; }

        public SearchWire.Status? Status { get; set; }

        public SearchWire.IssueType? IssueType { get; set; }

        public SearchWire.Priority? Priority { get; set; }

        public SearchWire.Assignee? Assignee { get; set; }

        public List<string>? Labels { get; set; }

        public SearchWire.Parent? Parent { get; set; }

        public string? Updated { get; set; }

        public SearchWire.Project? Project { get; set; }

        private Dictionary<string, ExtraValue>? Extra { get; set; }

        public void AddExtra(ReadOnlySpan<char> name, ExtraValue? value)
        {
            if (value is not null)
            {
                (Extra ??= [])[name.ToString()] = value;
            }
        }

        public SearchWire.Fields ToFields() =>
            new(Summary, Status, IssueType, Priority, Assignee, Labels, Parent, Updated, Project) { Extra = Extra };
    }
}
