using System.Text;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Edit.Models;

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
                WriteText(writer, issue.Description, issue.DescriptionIsMarkdown);
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

    private static void WriteExtraFields(Utf8JsonWriter writer, IReadOnlyDictionary<string, FieldUpdate>? fields)
    {
        foreach (var (fieldId, value) in fields ?? new Dictionary<string, FieldUpdate>())
        {
            writer.WritePropertyName(fieldId);
            writer.WriteRawValue(value.Json);
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

    private static void WriteText(Utf8JsonWriter writer, string text, bool isMarkdown)
    {
        if (isMarkdown)
        {
            AdfJsonWriter.Write(writer, AdfMarkdownParser.Parse(text));
        }
        else
        {
            WriteAdf(writer, text);
        }
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
