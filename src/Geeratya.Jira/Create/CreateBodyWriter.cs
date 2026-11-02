using System.Text;
using System.Text.Json;
using Geeratya.Jira.Create.Models;

namespace Geeratya.Jira.Create;

/// <summary>Builds the JSON body of <c>POST /issue</c>, including ADF for rich text. Pure.</summary>
internal static class CreateBodyWriter
{
    private const int AdfVersion = 1;
    private const string AdfDocType = "doc";
    private const string AdfParagraphType = "paragraph";
    private const string AdfTextType = "text";

    public static string Write(NewIssue issue)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("fields");
            WriteKeyed(writer, CreateFieldIds.Project, "key", issue.ProjectKey);
            WriteKeyed(writer, CreateFieldIds.IssueType, "id", issue.IssueTypeId);
            writer.WriteString(CreateFieldIds.Summary, issue.Summary.Trim());

            if (!string.IsNullOrWhiteSpace(issue.Description))
            {
                writer.WritePropertyName(CreateFieldIds.Description);
                WriteAdf(writer, issue.Description);
            }

            if (!string.IsNullOrWhiteSpace(issue.ParentKey))
            {
                WriteKeyed(writer, CreateFieldIds.Parent, "key", issue.ParentKey);
            }

            WriteExtraFields(writer, issue.Fields);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteExtraFields(Utf8JsonWriter writer, IReadOnlyDictionary<string, CreateFieldValue>? fields)
    {
        foreach (var (fieldId, value) in fields ?? new Dictionary<string, CreateFieldValue>())
        {
            writer.WritePropertyName(fieldId);
            WriteValue(writer, value);
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, CreateFieldValue value)
    {
        switch (value.Kind)
        {
            case CreateFieldValueKind.Text:
                writer.WriteStringValue(value.Values[0]);
                break;
            case CreateFieldValueKind.RichText:
                WriteAdf(writer, value.Values[0]);
                break;
            case CreateFieldValueKind.ChoiceId:
                WriteObjectWith(writer, "id", value.Values[0]);
                break;
            case CreateFieldValueKind.AccountId:
                WriteObjectWith(writer, "accountId", value.Values[0]);
                break;
            case CreateFieldValueKind.ChoiceIds:
                WriteArray(writer, value.Values, true);
                break;
            case CreateFieldValueKind.Strings:
                WriteArray(writer, value.Values, false);
                break;
            case CreateFieldValueKind.Number:
                writer.WriteRawValue(value.Values[0]);
                break;
        }
    }

    private static void WriteKeyed(Utf8JsonWriter writer, string field, string property, string value)
    {
        writer.WritePropertyName(field);
        WriteObjectWith(writer, property, value);
    }

    private static void WriteObjectWith(Utf8JsonWriter writer, string property, string value)
    {
        writer.WriteStartObject();
        writer.WriteString(property, value);
        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, IReadOnlyList<string> values, bool asIds)
    {
        writer.WriteStartArray();

        foreach (var value in values)
        {
            if (asIds)
            {
                WriteObjectWith(writer, "id", value);
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteAdf(Utf8JsonWriter writer, string text)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", AdfVersion);
        writer.WriteString("type", AdfDocType);
        writer.WriteStartArray("content");

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            writer.WriteStartObject();
            writer.WriteString("type", AdfParagraphType);

            // An empty line is an empty paragraph: ADF rejects a text node with no text.
            if (line.Length > 0)
            {
                writer.WriteStartArray("content");
                writer.WriteStartObject();
                writer.WriteString("type", AdfTextType);
                writer.WriteString("text", line);
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
