using System.Text.Json;

namespace Geeratya.Jira.Search;

/// <summary>
/// Hand-written readers for the small objects of a search row (status, type, assignee and so on). A nested
/// <c>JsonSerializer.Deserialize</c> call per object costs a read state each, which at 5000 rows outweighs what the
/// typed extra fields save; these walk the one reader instead. Every reader starts on the value token, ends on its
/// last token, returns null for <c>null</c> or a value of another shape, and skips what it does not know.
/// </summary>
internal static class WireObjectReader
{

    public static string? ReadText(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        _ = reader.TrySkip();

        return null;
    }

    public static List<string>? ReadLabels(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            _ = reader.TrySkip();

            return null;
        }

        var labels = new List<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (ReadText(ref reader) is { } label)
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    public static SearchWire.Status? ReadStatus(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? id = null;
        string? name = null;
        string? category = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Id))
            {
                reader.Read();
                id = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.Name))
            {
                reader.Read();
                name = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.StatusCategory))
            {
                reader.Read();
                category = ReadKey(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.Status(id, name, category is null ? null : new SearchWire.StatusCategory(category));
    }

    public static SearchWire.IssueType? ReadIssueType(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? id = null;
        string? name = null;
        int? level = null;
        bool? isSubtask = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Id))
            {
                reader.Read();
                id = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.Name))
            {
                reader.Read();
                name = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.HierarchyLevel))
            {
                reader.Read();
                level = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) ? number : null;
                SkipScalarMismatch(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.Subtask))
            {
                reader.Read();
                isSubtask = reader.TokenType is JsonTokenType.True or JsonTokenType.False ? reader.GetBoolean() : null;
                SkipScalarMismatch(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.IssueType(id, name, level, isSubtask);
    }

    public static SearchWire.Priority? ReadPriority(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? name = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Name))
            {
                reader.Read();
                name = ReadText(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.Priority(name);
    }

    public static SearchWire.Assignee? ReadAssignee(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? accountId = null;
        string? displayName = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.AccountId))
            {
                reader.Read();
                accountId = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(WireNames.DisplayName))
            {
                reader.Read();
                displayName = ReadText(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.Assignee(accountId, displayName);
    }

    public static SearchWire.Project? ReadProject(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? key = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Key))
            {
                reader.Read();
                key = ReadText(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.Project(key);
    }

    public static SearchWire.Parent? ReadParent(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? key = null;
        SearchWire.ParentStub? stub = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Key))
            {
                reader.Read();
                key = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(IssueFields.FieldsProperty))
            {
                reader.Read();
                stub = ReadParentStub(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.Parent(key, stub);
    }

    /// <summary>The <c>fields</c> of a sub-task stub: summary, status and type.</summary>
    public static ExtraSubtaskFields? ReadSubtaskFields(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? summary = null;
        SearchWire.Status? status = null;
        SearchWire.IssueType? type = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(IssueFields.Summary))
            {
                reader.Read();
                summary = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(IssueFields.Status))
            {
                reader.Read();
                status = ReadStatus(ref reader);
            }
            else if (reader.ValueTextEquals(IssueFields.IssueType))
            {
                reader.Read();
                type = ReadIssueType(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new ExtraSubtaskFields(summary, status, type);
    }

    /// <summary>Moves to the next property name of the current object; false at its end.</summary>
    public static bool NextProperty(ref Utf8JsonReader reader) =>
        reader.Read() && reader.TokenType == JsonTokenType.PropertyName;

    /// <summary>Moves past the value of the current property name, whatever its shape.</summary>
    public static void SkipValue(ref Utf8JsonReader reader)
    {
        reader.Read();
        _ = reader.TrySkip();
    }

    private static SearchWire.ParentStub? ReadParentStub(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? summary = null;
        SearchWire.IssueType? type = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(IssueFields.Summary))
            {
                reader.Read();
                summary = ReadText(ref reader);
            }
            else if (reader.ValueTextEquals(IssueFields.IssueType))
            {
                reader.Read();
                type = ReadIssueType(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new SearchWire.ParentStub(summary, type);
    }

    private static string? ReadKey(ref Utf8JsonReader reader)
    {
        if (!StartsObject(ref reader))
        {
            return null;
        }

        string? key = null;

        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals(WireNames.Key))
            {
                reader.Read();
                key = ReadText(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return key;
    }

    /// <summary>True on an object start; any other token (a <c>null</c>) is skipped and gives false.</summary>
    private static bool StartsObject(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return true;
        }

        _ = reader.TrySkip();

        return false;
    }

    /// <summary>A scalar of the wrong shape was already consumed as one token; an array or object is skipped whole.</summary>
    private static void SkipScalarMismatch(ref Utf8JsonReader reader) => _ = reader.TrySkip();
}
