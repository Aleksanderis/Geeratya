using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Writes;

/// <summary>The recorded write replies under <c>Fixtures/writes</c> (and the labelled synthetic ones).</summary>
public sealed partial class WriteFixtureTests
{
    private const string FixtureFolder = "writes";

    [Theory]
    [InlineData("comment-delete-gtm-reply", "DELETE", "/rest/api/3/issue/GTM-25/comment/10006")]
    [InlineData("issue-put-gtm-reply", "PUT", "/rest/api/3/issue/GTM-25")]
    [InlineData("transition-post-gtm-reply", "POST", "/rest/api/3/issue/GTM-25/transitions")]
    [InlineData("rank-put-gtm-reply", "PUT", "/rest/agile/1.0/issue/rank")]
    [InlineData("issue-delete-gtm-reply", "DELETE", "/rest/api/3/issue/GTM-25")]
    public async Task GivenRecordedEmptyReply_WhenWriting_ThenNoContentIsSuccess(string name, string method, string path)
    {
        using var fixture = new TransportFixture();
        var status = StatusOf(FixtureFiles.Read($"{FixtureFolder}/{name}.meta.txt"));
        fixture.Handler.Serve(new HttpMethod(method), path, FakeResponse.WithStatus(status));

        var result = await fixture.Transport.SendWriteAsync(
            JiraRequest.Write(new HttpMethod(method), path, "{}"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("create-gtm-task-reply", "key", "GTM-25")]
    [InlineData("comment-post-gtm-reply", "id", "10006")]
    [InlineData("comment-put-gtm-reply", "id", "10006")]
    public void GivenRecordedWriteReply_WhenParsed_ThenIdsAreThere(string name, string property, string expected)
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read($"{FixtureFolder}/{name}.json"));

        Assert.Equal(expected, document.RootElement.GetProperty(property).GetString());
    }

    [Theory]
    [InlineData("create-400-no-summary", "summary")]
    [InlineData("synthetic-transition-400-required-field", "resolution")]
    public async Task GivenFieldValidationReply_WhenWriting_ThenFieldIsKeyedById(string name, string field)
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read($"{FixtureFolder}/{name}.json");
        fixture.Handler.Serve(HttpMethod.Post, "/x", FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await fixture.Transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, "/x", "{}"),
            CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Equal(field, Assert.Single(result.Error.FieldErrors).FieldId);
    }

    [Fact]
    public void GivenSyntheticTransition_WhenRead_ThenItAsksForARequiredResolutionWithChoices()
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read($"{FixtureFolder}/synthetic-transitions-required-field.json"));
        var transition = document.RootElement.GetProperty("transitions")[0];
        var resolution = transition.GetProperty("fields").GetProperty("resolution");

        Assert.True(transition.GetProperty("hasScreen").GetBoolean());
        Assert.True(resolution.GetProperty("required").GetBoolean());
        Assert.True(resolution.GetProperty("allowedValues").GetArrayLength() >= 2);
    }

    [Fact]
    public void GivenWriteFixtures_WhenScanned_ThenOnlyScrubbedSiteDataRemains()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureFolder);
        var files = Directory.GetFiles(folder);

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            foreach (Match match in HostPattern().Matches(text))
            {
                Assert.Equal("example.atlassian.net", match.Value.ToLowerInvariant());
            }

            foreach (Match match in EmailPattern().Matches(text))
            {
                Assert.EndsWith("@example.com", match.Value, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [GeneratedRegex(@"[a-z0-9-]+\.atlassian\.net", RegexOptions.IgnoreCase)]
    private static partial Regex HostPattern();

    [GeneratedRegex(@"[A-Za-z0-9._-]+@[A-Za-z0-9.-]+")]
    private static partial Regex EmailPattern();

    private static HttpStatusCode StatusOf(string meta)
    {
        var line = meta.Split('\n')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return (HttpStatusCode)int.Parse(line[1]);
    }
}
