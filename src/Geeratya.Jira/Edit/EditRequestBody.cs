using System.Buffers;
using System.Text;
using System.Text.Json;
using Geeratya.Jira.Edit.Models;

namespace Geeratya.Jira.Edit;

/// <summary>Builds the bodies of <c>PUT /issue/{key}</c> and <c>PUT /issue/{key}/assignee</c> (no reflection).</summary>
internal static class EditRequestBody
{
    private const string AutomaticAccountId = "-1";

    public static string BuildUpdate(IReadOnlyList<FieldUpdate> updates)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            WriteSection(writer, "fields", updates.Where(update => !update.IsOperation));
            WriteSection(writer, "update", updates.Where(update => update.IsOperation));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static string BuildAssignee(AssigneeChange change)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            switch (change)
            {
                case AssigneeChange.AccountChange account:
                    writer.WriteString("accountId", account.AccountId);
                    break;
                case AssigneeChange.AutomaticChange:
                    writer.WriteString("accountId", AutomaticAccountId);
                    break;
                default:
                    writer.WriteNull("accountId");
                    break;
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteSection(Utf8JsonWriter writer, string name, IEnumerable<FieldUpdate> updates)
    {
        var section = updates.ToList();

        if (section.Count == 0)
        {
            return;
        }

        writer.WriteStartObject(name);

        foreach (var update in section)
        {
            writer.WritePropertyName(update.FieldId);
            writer.WriteRawValue(update.Json);
        }

        writer.WriteEndObject();
    }
}
