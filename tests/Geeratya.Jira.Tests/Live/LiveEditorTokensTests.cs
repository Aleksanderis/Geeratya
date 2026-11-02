using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments;
using Geeratya.Jira.Edit;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// Issue #340: what the comment editor's inline suggestions write (a mention of a real account and a link to an
/// issue) is stored and read back by the dev site without loss, in a company-managed (<c>GCM</c>) and a
/// team-managed (<c>GTM</c>) project, next to an inline card for the same issue, and the stored comment goes through
/// the editor's text and back unchanged. Each run makes one issue labelled <c>geeratya-test-&lt;run id&gt;</c>;
/// <c>GCM</c> refuses deletes (#19), so that issue stays.
/// </summary>
public class LiveEditorTokensTests
{
    private const string IssuePath = "/rest/api/3/issue";

    [LiveFact]
    public async Task GivenMentionAndIssueLinkInGtm_WhenStoredAndReadBack_ThenBothSurviveTheEditorRoundTrip()
    {
        await CheckAsync("GTM");
    }

    [LiveFact]
    public async Task GivenMentionAndIssueLinkInGcm_WhenStoredAndReadBack_ThenBothSurviveTheEditorRoundTrip()
    {
        await CheckAsync("GCM");
    }

    private static async Task CheckAsync(string project)
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var token = CancellationToken.None;
        var runId = Guid.NewGuid().ToString("N")[..8];
        var me = await transport.SendAsync(
            JiraRequest.Get("/rest/api/3/myself"),
            LiveWriteJsonContext.Default.LiveAccount,
            token);
        Assert.True(me.IsSuccess);
        var body = $$$"""{"fields":{"project":{"key":"{{{project}}}"},"issuetype":{"name":"Task"},"summary":"geeratya editor tokens live","labels":["geeratya-test-{{{runId}}}"]}}""";
        var created = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, IssuePath, body),
            LiveWriteJsonContext.Default.LiveCreatedIssue,
            token);
        Assert.True(created.IsSuccess);
        var key = created.Value.Key;

        try
        {
            var site = $"https://{environment.Connection.SiteUrl.Host}";
            var url = $"{site}/browse/{key}";
            var typed = $"cc {AdfMarkdownTokens.Mention(me.Value.AccountId, "Me")} see {AdfMarkdownTokens.IssueLink(site, key)}";
            var client = new CommentClient(transport);

            var linked = await client.AddCommentAsync(key, AdfMarkdownParser.Parse(typed), token);
            Assert.True(linked.IsSuccess);
            var card = await client.AddCommentAsync(key, InlineCardBody(url), token);
            Assert.True(card.IsSuccess);

            var read = await client.GetCommentsAsync(key, 0, token);
            Assert.True(read.IsSuccess);
            var stored = read.Value.Items.Single(comment => comment.Id == linked.Value.Id).Body;
            var storedCard = read.Value.Items.Single(comment => comment.Id == card.Value.Id).Body;

            var nodes = Flatten(stored).ToList();
            Assert.Contains(nodes, node => node is AdfMention mention && mention.Id == me.Value.AccountId);
            Assert.Contains(
                nodes,
                node => node is AdfText { Marks: [AdfLinkMark mark] } && mark.Href == url);
            Assert.Contains(Flatten(storedCard), node => node is AdfInlineCard inline && inline.Url == url);

            Assert.True(AdfRoundTrip.RoundTrips(stored));
            Assert.Contains(AdfMarkdownTokens.IssueLink(site, key), AdfMarkdownWriter.ToText(stored), StringComparison.Ordinal);
            Assert.Contains($"(mention:{me.Value.AccountId})", AdfMarkdownWriter.ToText(stored), StringComparison.Ordinal);
        }
        finally
        {
            // GCM rejects deletes with 403 (#19), so the issue may stay there.
            await new EditClient(transport).DeleteIssueAsync(key, false, token);
        }
    }

    private static AdfDocument InlineCardBody(string url) =>
        AdfParser.Parse(
            $$$"""{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"inlineCard","attrs":{"url":"{{{url}}}"}}]}]}""");

    private static IEnumerable<AdfNode> Flatten(AdfDocument document) => document.Content.SelectMany(Flatten);

    private static IEnumerable<AdfNode> Flatten(AdfNode node)
    {
        yield return node;

        foreach (var child in (node as AdfContainerNode)?.Children.SelectMany(Flatten) ?? [])
        {
            yield return child;
        }
    }
}
