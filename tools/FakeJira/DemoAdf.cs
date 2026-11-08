using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

/// <summary>
/// Turns a small text markup into an ADF document, so the demo dataset can be written as readable text: blocks are
/// separated by blank lines; "## " starts a heading, lines with "- " a bullet list, lines with "1. " a numbered list,
/// a block between ``` lines is a code block, anything else a paragraph. Inline: **bold** and `code`.
/// </summary>
public static partial class DemoAdf
{
    [GeneratedRegex(@"(\*\*[^*]+\*\*|`[^`]+`)")]
    private static partial Regex InlineMarks();

    public static JsonObject Document(string markup)
    {
        var content = new JsonArray();
        var lines = markup.ReplaceLineEndings("\n").Trim('\n').Split('\n');
        var index = 0;

        while (index < lines.Length)
        {
            if (lines[index].Trim().Length == 0)
            {
                index++;

                continue;
            }

            if (lines[index].StartsWith("```", StringComparison.Ordinal))
            {
                var code = new List<string>();
                index++;

                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    code.Add(lines[index++]);
                }

                index++;
                content.Add((JsonNode)CodeBlock(string.Join('\n', code)));

                continue;
            }

            var block = new List<string>();

            while (index < lines.Length && lines[index].Trim().Length > 0
                && !lines[index].StartsWith("```", StringComparison.Ordinal))
            {
                block.Add(lines[index++]);
            }

            content.Add((JsonNode)Block(block));
        }

        return new JsonObject { ["version"] = 1, ["type"] = "doc", ["content"] = content };
    }

    private static JsonObject Block(List<string> lines)
    {
        if (lines[0].StartsWith("## ", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["type"] = "heading",
                ["attrs"] = new JsonObject { ["level"] = 3 },
                ["content"] = Inline(lines[0][3..]),
            };
        }

        if (lines[0].StartsWith("- ", StringComparison.Ordinal))
        {
            return List("bulletList", lines.Select(line => line[2..]));
        }

        if (NumberedItem().IsMatch(lines[0]))
        {
            return List("orderedList", lines.Select(line => NumberedItem().Replace(line, string.Empty)));
        }

        return new JsonObject { ["type"] = "paragraph", ["content"] = Inline(string.Join(' ', lines)) };
    }

    [GeneratedRegex(@"^\d+\.\s+")]
    private static partial Regex NumberedItem();

    private static JsonObject List(string type, IEnumerable<string> items)
    {
        var entries = new JsonArray();

        foreach (var item in items)
        {
            JsonNode paragraph = new JsonObject { ["type"] = "paragraph", ["content"] = Inline(item) };
            JsonNode entry = new JsonObject { ["type"] = "listItem", ["content"] = new JsonArray(paragraph) };
            entries.Add(entry);
        }

        return new JsonObject { ["type"] = type, ["content"] = entries };
    }

    private static JsonObject CodeBlock(string code)
    {
        return new JsonObject
        {
            ["type"] = "codeBlock",
            ["attrs"] = new JsonObject { ["language"] = "text" },
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = code }),
        };
    }

    private static JsonArray Inline(string text)
    {
        var result = new JsonArray();

        foreach (var part in InlineMarks().Split(text).Where(part => part.Length > 0))
        {
            var bold = part.StartsWith("**", StringComparison.Ordinal);
            var code = !bold && part.StartsWith('`');
            var node = new JsonObject { ["type"] = "text", ["text"] = bold ? part[2..^2] : code ? part[1..^1] : part };

            if (bold || code)
            {
                node["marks"] = new JsonArray(new JsonObject { ["type"] = bold ? "strong" : "code" });
            }

            result.Add((JsonNode)node);
        }

        return result;
    }
}
