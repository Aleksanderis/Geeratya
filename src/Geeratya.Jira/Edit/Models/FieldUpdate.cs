using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Geeratya.Jira.Adf;

namespace Geeratya.Jira.Edit.Models;

/// <summary>
/// One change to one field of an issue, built by the factory for the field's schema type (see
/// <see cref="EditField.Kind"/>). A factory taking a nullable value clears the field when given null. The value is
/// already JSON in the shape Jira wants, so a wrong shape cannot be built from the outside.
/// A replace is sent under <c>fields</c>, an add/remove change under <c>update</c> (see <see cref="IsOperation"/>).
/// </summary>
public sealed class FieldUpdate
{
    private const string IdProperty = "id";
    private const string AddOperation = "add";
    private const string RemoveOperation = "remove";
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

    private FieldUpdate(string fieldId, bool isOperation, string json)
    {
        FieldId = fieldId;
        IsOperation = isOperation;
        Json = json;
    }

    /// <summary>The field to change.</summary>
    public string FieldId { get; }

    /// <summary>True for add/remove operations (sent under <c>update</c>), false for a replace (<c>fields</c>).</summary>
    public bool IsOperation { get; }

    internal string Json { get; }

    /// <summary>A single-line or plain multi-line text field. Null clears it.</summary>
    public static FieldUpdate Text(string fieldId, string? value) => Replace(fieldId, writer =>
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    });

    /// <summary>A rich-text field (description, environment, text areas): ADF in API v3. Null clears it.</summary>
    public static FieldUpdate RichText(string fieldId, AdfDocument? value) => Replace(fieldId, writer =>
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            AdfJsonWriter.Write(writer, value);
        }
    });

    /// <summary>A number field (story points). Null clears it.</summary>
    public static FieldUpdate Number(string fieldId, double? value) => Replace(fieldId, writer =>
    {
        if (value is { } number && double.IsFinite(number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteNullValue();
        }
    });

    /// <summary>A date field (due date), written as <c>yyyy-MM-dd</c>. Null clears it.</summary>
    public static FieldUpdate Date(string fieldId, DateOnly? value) =>
        Replace(fieldId, writer => WriteNullable(writer, value?.ToString(DateFormat, CultureInfo.InvariantCulture)));

    /// <summary>A date-time field, written with its offset the way Jira reads it (<c>+0200</c>). Null clears it.</summary>
    public static FieldUpdate DateTime(string fieldId, DateTimeOffset? value) =>
        Replace(fieldId, writer => WriteNullable(writer, FormatDateTime(value)));

    /// <summary>A single choice (priority, resolution, select list) by the id from its allowed values. Null clears.</summary>
    public static FieldUpdate Option(string fieldId, string? optionId) => Replace(fieldId, writer =>
    {
        if (string.IsNullOrEmpty(optionId))
        {
            writer.WriteNullValue();
        }
        else
        {
            WriteIdObject(writer, optionId);
        }
    });

    /// <summary>Replaces a multi-choice field (components, versions, checkboxes) with these ids; empty clears it.</summary>
    public static FieldUpdate Options(string fieldId, IReadOnlyList<string> optionIds) => Replace(fieldId, writer =>
    {
        writer.WriteStartArray();

        foreach (var optionId in optionIds)
        {
            WriteIdObject(writer, optionId);
        }

        writer.WriteEndArray();
    });

    /// <summary>Adds and removes entries of a multi-choice field by id, leaving the others as they are.</summary>
    public static FieldUpdate ChangeOptions(
        string fieldId,
        IReadOnlyList<string> addIds,
        IReadOnlyList<string> removeIds) => Operation(fieldId, writer =>
    {
        foreach (var optionId in addIds)
        {
            WriteOperation(writer, AddOperation, () => WriteIdObject(writer, optionId));
        }

        foreach (var optionId in removeIds)
        {
            WriteOperation(writer, RemoveOperation, () => WriteIdObject(writer, optionId));
        }
    });

    /// <summary>A user picker field other than the assignee, by account id. Null clears it.</summary>
    public static FieldUpdate User(string fieldId, string? accountId) =>
        Replace(fieldId, writer => WriteUser(writer, accountId));

    /// <summary>Replaces the labels with these. Labels are plain strings and cannot hold spaces.</summary>
    public static FieldUpdate Labels(string fieldId, IReadOnlyList<string> labels) => Replace(fieldId, writer =>
    {
        writer.WriteStartArray();

        foreach (var label in labels)
        {
            writer.WriteStringValue(label);
        }

        writer.WriteEndArray();
    });

    /// <summary>Adds and removes labels, leaving the others as they are.</summary>
    public static FieldUpdate ChangeLabels(
        string fieldId,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove) => Operation(fieldId, writer =>
    {
        foreach (var label in add)
        {
            WriteOperation(writer, AddOperation, label);
        }

        foreach (var label in remove)
        {
            WriteOperation(writer, RemoveOperation, label);
        }
    });

    /// <summary>The parent (epic or parent issue) by key. Null clears the link.</summary>
    public static FieldUpdate Parent(string fieldId, string? parentKey) => Replace(fieldId, writer =>
    {
        if (string.IsNullOrEmpty(parentKey))
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStartObject();
        writer.WriteString("key", parentKey);
        writer.WriteEndObject();
    });

    internal static void WriteUser(Utf8JsonWriter writer, string? accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStartObject();
        writer.WriteString("accountId", accountId);
        writer.WriteEndObject();
    }

    private static FieldUpdate Replace(string fieldId, Action<Utf8JsonWriter> write) =>
        new(fieldId, false, Render(write));

    private static FieldUpdate Operation(string fieldId, Action<Utf8JsonWriter> writeOperations) =>
        new(fieldId, true, Render(writer =>
        {
            writer.WriteStartArray();
            writeOperations(writer);
            writer.WriteEndArray();
        }));

    private static string Render(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteNullable(Utf8JsonWriter writer, string? text)
    {
        if (text is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(text);
        }
    }

    private static void WriteIdObject(Utf8JsonWriter writer, string id)
    {
        writer.WriteStartObject();
        writer.WriteString(IdProperty, id);
        writer.WriteEndObject();
    }

    private static void WriteOperation(Utf8JsonWriter writer, string operation, string text)
    {
        writer.WriteStartObject();
        writer.WriteString(operation, text);
        writer.WriteEndObject();
    }

    private static void WriteOperation(Utf8JsonWriter writer, string operation, Action writeValue)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(operation);
        writeValue();
        writer.WriteEndObject();
    }

    private static string? FormatDateTime(DateTimeOffset? value)
    {
        if (value is not { } moment)
        {
            return null;
        }

        var offset = moment.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var local = moment.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

        return $"{local}{sign}{Math.Abs(offset.Hours):00}{Math.Abs(offset.Minutes):00}";
    }
}
