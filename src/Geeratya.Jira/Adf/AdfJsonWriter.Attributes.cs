using System.Globalization;
using System.Text.Json;

namespace Geeratya.Jira.Adf;

public static partial class AdfJsonWriter
{
    private static void WritePlain(WriteState state, string type, IReadOnlyList<AdfNode> children)
    {
        WriteStart(state.Writer, type);
        WriteContent(state, children);
        state.Writer.WriteEndObject();
    }

    private static void WriteEmpty(Utf8JsonWriter writer, string type)
    {
        WriteStart(writer, type);
        writer.WriteEndObject();
    }

    private static void WriteStart(Utf8JsonWriter writer, string type)
    {
        writer.WriteStartObject();
        writer.WriteString(AdfPropertyNames.Type, type);
    }

    private static void WriteAttributes(Utf8JsonWriter writer, string name, int value)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteNumber(name, value);
        writer.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(name, value);
        writer.WriteEndObject();
    }

    private static void WriteOptionalAttribute(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            WriteAttributes(writer, name, value);
        }
    }

    private static void WriteTwoAttributes(
        Utf8JsonWriter writer,
        string firstName,
        string? firstValue,
        string secondName,
        string? secondValue)
    {
        if (firstValue is null && secondValue is null)
        {
            return;
        }

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        WriteIfPresent(writer, firstName, firstValue);
        WriteIfPresent(writer, secondName, secondValue);
        writer.WriteEndObject();
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteLocalId(WriteState state, string prefix)
    {
        state.LocalIdCount++;

        WriteAttributes(
            state.Writer,
            LocalIdProperty,
            $"{prefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
    }

    private static void WriteTaskItemAttributes(WriteState state, AdfTaskItem item)
    {
        state.LocalIdCount++;

        var writer = state.Writer;

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(
            LocalIdProperty,
            $"{TaskItemIdPrefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteString(
            AdfAttributeNames.State,
            item.State == AdfTaskState.Done ? AdfTaskStateNames.Done : AdfTaskStateNames.Todo);
        writer.WriteEndObject();
    }

    private static void WriteDecisionItemAttributes(WriteState state, AdfDecisionItem item)
    {
        state.LocalIdCount++;

        var writer = state.Writer;

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(
            LocalIdProperty,
            $"{DecisionItemIdPrefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteString(AdfAttributeNames.State, item.State ?? string.Empty);
        writer.WriteEndObject();
    }

    private static void WriteCellAttributes(
        Utf8JsonWriter writer,
        int columnSpan,
        int rowSpan,
        IReadOnlyList<int> columnWidths)
    {
        if (columnSpan == 1 && rowSpan == 1 && columnWidths.Count == 0)
        {
            return;
        }

        writer.WriteStartObject(AdfPropertyNames.Attributes);

        if (columnSpan != 1)
        {
            writer.WriteNumber(AdfAttributeNames.ColumnSpan, columnSpan);
        }

        if (rowSpan != 1)
        {
            writer.WriteNumber(AdfAttributeNames.RowSpan, rowSpan);
        }

        if (columnWidths.Count > 0)
        {
            writer.WriteStartArray(AdfAttributeNames.ColumnWidth);

            for (var index = 0; index < columnWidths.Count; index++)
            {
                writer.WriteNumberValue(columnWidths[index]);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteMediaAttributes(Utf8JsonWriter writer, AdfMedia media)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        WriteIfPresent(writer, AdfAttributeNames.Id, media.Id);
        WriteIfPresent(writer, AdfPropertyNames.Type, media.MediaType);
        WriteIfPresent(writer, AdfAttributeNames.Collection, media.Collection);
        WriteIfPresent(writer, AdfAttributeNames.Alt, media.Alt);

        if (media.Width is { } width)
        {
            writer.WriteNumber(AdfAttributeNames.Width, width);
        }

        if (media.Height is { } height)
        {
            writer.WriteNumber(AdfAttributeNames.Height, height);
        }

        writer.WriteEndObject();
    }
}
