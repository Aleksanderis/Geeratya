using System.Text.Json;
using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Tests.Adf;

/// <summary>Helpers shared by the ADF renderer tests: documents from JSON, fixtures, plain text of the output.</summary>
internal static class AdfTestSupport
{
    public const string RichDescription = "issue-gtm5-rich-description.json";
    public const string MediaDescription = "issue-gcm-media-description.json";

    public static string FixtureJson(string fixture)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "adf", fixture));
    }

    public static string GoldenText(string golden, int width)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", "Adf", $"{golden}-w{width}.txt");

        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }

    public static AdfDocument FixtureDescription(string fixture)
    {
        using var document = JsonDocument.Parse(FixtureJson(fixture));

        return AdfParser.Parse(document.RootElement.GetProperty("fields").GetProperty("description"));
    }

    /// <summary>A document from the JSON of its content nodes.</summary>
    public static AdfDocument Document(string contentJson)
    {
        return AdfParser.Parse($$"""{"version":1,"type":"doc","content":[{{contentJson}}]}""");
    }

    public static string[] Lines(string contentJson, int width)
    {
        return PlainLines(AdfRenderer.Render(Document(contentJson), width));
    }

    public static string[] PlainLines(AdfRendering rendering)
    {
        return [.. rendering.Lines.Select(static line => line.PlainText)];
    }

    public static StyledSpan[] Spans(string contentJson, int width)
    {
        var rendering = AdfRenderer.Render(Document(contentJson), width);

        return [.. rendering.Lines.SelectMany(static line => line.Spans)];
    }

    public static string Paragraph(string text)
    {
        return $$"""{"type":"paragraph","content":[{"type":"text","text":"{{text}}"}]}""";
    }
}
