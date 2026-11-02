using Geeratya.Core.Jump;

namespace Geeratya.Core.Tests.Jump;

[Trait("Os", "Independent")]
public class JumpKeyTests
{
    [Theory]
    [InlineData("GCM-12", "GCM-12")]
    [InlineData("gcm-12", "GCM-12")]
    [InlineData("  Gcm-7  ", "GCM-7")]
    [InlineData("a1_b-3", "A1_B-3")]
    public void GivenAKey_WhenParsing_ThenItIsUpperCased(string text, string expected)
    {
        var parse = JumpKey.Parse(text, null);

        Assert.Equal(JumpParseKind.Key, parse.Kind);
        Assert.Equal(expected, parse.Key);
    }

    [Theory]
    [InlineData("12")]
    [InlineData(" 12 ")]
    public void GivenABareNumberAndAProject_WhenParsing_ThenTheProjectCompletesIt(string text)
    {
        var parse = JumpKey.Parse(text, "GCM");

        Assert.Equal(JumpParseKind.Key, parse.Kind);
        Assert.Equal("GCM-12", parse.Key);
    }

    [Fact]
    public void GivenABareNumberAndNoProject_WhenParsing_ThenItNeedsAProject()
    {
        Assert.Equal(JumpParseKind.NeedsProject, JumpKey.Parse("12", null).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenNothingTyped_WhenParsing_ThenItIsEmpty(string text)
    {
        Assert.Equal(JumpParseKind.Empty, JumpKey.Parse(text, "GCM").Kind);
    }

    [Theory]
    [InlineData("gcm")]
    [InlineData("GCM-")]
    [InlineData("-12")]
    [InlineData("GCM-12 x")]
    [InlineData("GCM-1/../2")]
    [InlineData("GCM-１２")]
    [InlineData("1GCM-12")]
    public void GivenGarbage_WhenParsing_ThenItIsInvalid(string text)
    {
        Assert.Equal(JumpParseKind.Invalid, JumpKey.Parse(text, "GCM").Kind);
    }

    [Theory]
    [InlineData("GCM-12", "GCM")]
    [InlineData("A-1", "A")]
    [InlineData("nokey", null)]
    public void GivenAKey_WhenTakingItsProject_ThenItIsTheTextBeforeTheNumber(string key, string? expected)
    {
        Assert.Equal(expected, JumpKey.ProjectOf(key));
    }
}
