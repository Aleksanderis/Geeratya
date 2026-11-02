using Geeratya.Jira.Adf;

namespace Geeratya.Jira.Tests.Adf;

[Trait("Os", "Independent")]
public sealed class AdfMarkdownTokensTests
{
    [Fact]
    public void GivenAMentionToken_WhenParsed_ThenItIsAMentionNodeWithTheAccountId()
    {
        var token = AdfMarkdownTokens.Mention("acc-1", "Olga [Ops]");

        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(AdfMarkdownParser.Parse($"hi {token}").Content));

        Assert.Contains(paragraph.Children, node => node is AdfMention { Id: "acc-1", Text: "@Olga [Ops]" });
    }

    [Fact]
    public void GivenAnIssueLinkToken_WhenParsed_ThenItIsATextWithALinkMarkToTheBrowsePage()
    {
        var token = AdfMarkdownTokens.IssueLink("https://x.atlassian.net/", "GCM-12");

        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(AdfMarkdownParser.Parse(token).Content));

        Assert.Equal("[GCM-12](https://x.atlassian.net/browse/GCM-12)", token);
        Assert.Contains(
            paragraph.Children,
            node => node is AdfText { Text: "GCM-12", Marks: [AdfLinkMark { Href: "https://x.atlassian.net/browse/GCM-12" }] });
    }

    [Fact]
    public void GivenTextWithTokensAndLookAlikes_WhenFinding_ThenOnlyRealTokensAreReported()
    {
        var mention = AdfMarkdownTokens.Mention("acc-1", "Olga");
        var issue = AdfMarkdownTokens.IssueLink("https://x.atlassian.net", "A-1");
        var text = $"[docs](https://x.atlassian.net/page) a@b.c \\@[x](mention:y) {mention} {issue} @[open";

        var spans = AdfMarkdownTokens.Find(text);

        Assert.Equal(2, spans.Count);
        Assert.Equal(mention, text[spans[0].Start..spans[0].End]);
        Assert.Equal(issue, text[spans[1].Start..spans[1].End]);
    }

    [Fact]
    public void GivenAnInvalidKeyOrASitePath_WhenBuildingALink_ThenTheKeyStaysPlainAndOnlyTheHostIsUsed()
    {
        Assert.Equal("GCM-1/../x", AdfMarkdownTokens.IssueLink("https://x.atlassian.net", "GCM-1/../x"));
        Assert.Equal(
            "[A-1](https://x.atlassian.net/browse/A-1)",
            AdfMarkdownTokens.IssueLink("https://x.atlassian.net/ex/jira/abc", "A-1"));
    }

    [Fact]
    public void GivenALinkWhoseTextIsNotTheKeyInItsAddress_WhenFinding_ThenItIsPlainLinkText()
    {
        Assert.Empty(AdfMarkdownTokens.Find("[GCM-1](https://x.atlassian.net/browse/GCM-2)"));
    }
}
