using System.Text.Json;
using Geeratya.Core.Settings;

namespace Geeratya.Core.Tests.Settings;

[Trait("Os", "Independent")]
public sealed class SettingsTextTests
{
    private const string WindowsNewline = "\u000d\u000a";

    private static readonly JsonDocumentOptions _lenientOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void GivenAComment_WhenChangingAValue_ThenOnlyTheValueChanges()
    {
        const string text = "{\n  // keep me\n  \"whichKey\": true, /* inline */\n  \"theme\": \"auto\",\n}\n";

        var edit = SettingsText.Set(text, "whichKey", "false");

        Assert.Equal(text.Replace("true", "false"), edit.Text);
    }

    [Fact]
    public void GivenADuplicateKey_WhenChangingIt_ThenTheLastOneChanges()
    {
        var edit = SettingsText.Set("""{ "theme": "a", "theme": "b" }""", "theme", "\"c\"");

        Assert.Equal("""{ "theme": "a", "theme": "c" }""", edit.Text);
    }

    [Fact]
    public void GivenANestedKeyWithTheSameName_WhenChangingTheTopLevelOne_ThenTheNestedOneIsKept()
    {
        var edit = SettingsText.Set("""{ "other": { "theme": "x" }, "theme": "a" }""", "theme", "\"c\"");

        Assert.Equal("""{ "other": { "theme": "x" }, "theme": "c" }""", edit.Text);
    }

    [Fact]
    public void GivenAMissingKey_WhenChangingIt_ThenItIsAddedAsTheLastMemberWithTheSameIndent()
    {
        const string text = "{\n    // note\n    \"theme\": \"auto\"\n}\n";

        var edit = SettingsText.Set(text, "whichKey", "false");

        Assert.Equal("{\n    // note\n    \"theme\": \"auto\",\n    \"whichKey\": false\n}\n", edit.Text);
    }

    [Fact]
    public void GivenATrailingComma_WhenAddingAKey_ThenTheFileStaysValid()
    {
        var edit = SettingsText.Set("{\n  \"theme\": \"auto\",\n}\n", "whichKey", "false");

        Assert.Equal("{\n  \"theme\": \"auto\",\n  \"whichKey\": false,\n}\n", edit.Text);
        Assert.False(ReadBack(edit.Text!));
    }

    [Fact]
    public void GivenWindowsLineEndings_WhenAddingAKey_ThenTheInsertedLineUsesThem()
    {
        var source = "{\n  \"theme\": \"auto\"\n}\n".Replace("\n", WindowsNewline);
        var expected = "{\n  \"theme\": \"auto\",\n  \"whichKey\": false\n}\n".Replace("\n", WindowsNewline);

        var edit = SettingsText.Set(source, "whichKey", "false");

        Assert.Equal(expected, edit.Text);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ }")]
    [InlineData("{\n}\n")]
    public void GivenAnEmptyObject_WhenAddingAKey_ThenTheMemberFillsIt(string text)
    {
        var edit = SettingsText.Set(text, "whichKey", "false");

        Assert.False(ReadBack(edit.Text!));
    }

    [Fact]
    public void GivenAnObjectWithOnlyAComment_WhenAddingAKey_ThenTheCommentStays()
    {
        var edit = SettingsText.Set("{ // hi\n}", "whichKey", "false");

        Assert.Contains("// hi", edit.Text);
        Assert.False(ReadBack(edit.Text!));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"a\": 1 } trailing")]
    public void GivenAFileThatDoesNotParse_WhenChanging_ThenItIsRefusedWithTheLine(string text)
    {
        var edit = SettingsText.Set($"\n{text}", "whichKey", "false");

        Assert.Null(edit.Text);
        Assert.Contains("not valid JSON (line 2)", edit.Error);
    }

    [Fact]
    public void GivenAnArray_WhenChanging_ThenItIsRefused()
    {
        var edit = SettingsText.Set("[1]", "whichKey", "false");

        Assert.Null(edit.Text);
        Assert.Contains("not a JSON object", edit.Error);
    }

    [Fact]
    public void GivenNonAsciiText_WhenChanging_ThenItSurvives()
    {
        var edit = SettingsText.Set("{ \"note\": \"žąčę\", \"theme\": \"a\" }", "theme", "\"b\"");

        Assert.Equal("{ \"note\": \"žąčę\", \"theme\": \"b\" }", edit.Text);
    }

    private static bool ReadBack(string text)
    {
        using var document = JsonDocument.Parse(text, _lenientOptions);

        return document.RootElement.GetProperty("whichKey").GetBoolean();
    }
}
