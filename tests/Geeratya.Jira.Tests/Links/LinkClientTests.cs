using System.Net;
using System.Text.Json;
using Geeratya.Jira.Links;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Links;

public sealed class LinkClientTests
{
    private const string TypesPath = "/rest/api/3/issueLinkType";
    private const string LinkPath = "/rest/api/3/issueLink";

    [Fact]
    public async Task GivenRecordedLinkTypes_WhenReading_ThenEveryTypeKeepsItsIdNameAndBothWordings()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("links/issuelink-types.json");
        fixture.Handler.Serve(HttpMethod.Get, TypesPath, FakeResponse.WithStatus(HttpStatusCode.OK, body));

        var result = await new LinkClient(fixture.Transport).GetLinkTypesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["Blocks", "Cloners", "Duplicate", "Problem/Incident", "Relates"],
            result.Value.Select(type => type.Name));
        var blocks = result.Value[0];
        Assert.Equal("10000", blocks.Id);
        Assert.Equal("is blocked by", blocks.Inward);
        Assert.Equal("blocks", blocks.Outward);
    }

    [Fact]
    public async Task GivenTypesWithoutIdOrNameAndNoWordings_WhenReading_ThenTheyAreSkippedOrFallBackToTheName()
    {
        using var fixture = new TransportFixture();
        const string body = """{"issueLinkTypes":[{"name":"NoId"},{"id":"1"},null,{"id":"2","name":"Bare"}]}""";
        fixture.Handler.Serve(HttpMethod.Get, TypesPath, FakeResponse.WithStatus(HttpStatusCode.OK, body));

        var result = await new LinkClient(fixture.Transport).GetLinkTypesAsync(CancellationToken.None);

        var bare = Assert.Single(result.Value);
        Assert.Equal(("Bare", "Bare"), (bare.Inward, bare.Outward));
    }

    [Theory]
    [InlineData("GCM-3", "GCM-5", "GCM-3", "GCM-5")]
    [InlineData("GTM-6", "GTM-5", "GTM-6", "GTM-5")]
    public async Task GivenOutwardAndInwardKeys_WhenCreating_ThenOnePostNamesTheTypeIdAndEachSide(
        string outward,
        string inward,
        string expectedOutward,
        string expectedInward)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, LinkPath, FakeResponse.WithStatus(HttpStatusCode.Created));

        var result = await new LinkClient(fixture.Transport)
            .CreateLinkAsync("10000", outward, inward, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("10000", body.RootElement.GetProperty("type").GetProperty("id").GetString());
        Assert.Equal(expectedOutward, body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString());
        Assert.Equal(expectedInward, body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString());
    }

    [Theory]
    [InlineData("10000", "gcm-3", "GCM-5")]
    [InlineData("10000", "GCM-3", "not a key")]
    [InlineData("10000", "GCM-3", "")]
    public async Task GivenAMalformedKey_WhenCreating_ThenItIsRefusedAtTheIssueFieldWithoutARequest(
        string type,
        string outward,
        string inward)
    {
        using var fixture = new TransportFixture();

        var result = await new LinkClient(fixture.Transport)
            .CreateLinkAsync(type, outward, inward, CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Equal(LinkClient.IssueFieldId, Assert.Single(result.Error.FieldErrors).FieldId);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenTheSameKeyTwice_WhenCreating_ThenItIsRefusedWithoutARequest()
    {
        using var fixture = new TransportFixture();

        var result = await new LinkClient(fixture.Transport)
            .CreateLinkAsync("10000", "GCM-3", "GCM-3", CancellationToken.None);

        Assert.Contains("itself", result.Error.Message);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenNoType_WhenCreating_ThenItIsRefusedWithoutARequest()
    {
        using var fixture = new TransportFixture();

        var result = await new LinkClient(fixture.Transport)
            .CreateLinkAsync(" ", "GCM-3", "GCM-5", CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.NotFound, WriteErrorKind.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, WriteErrorKind.Validation)]
    public async Task GivenARefusedLink_WhenCreating_ThenItFailsDefinitelyAndIsSentOnce(
        HttpStatusCode status,
        WriteErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, LinkPath, FakeResponse.WithStatus(status, "{}"));

        var result = await new LinkClient(fixture.Transport)
            .CreateLinkAsync("10000", "GCM-3", "GCM-5", CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenALinkId_WhenDeleting_ThenOneDeleteRequestWithoutABodyIsSent()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(HttpStatusCode.NoContent);

        fixture.Handler.Serve(HttpMethod.Delete, $"{LinkPath}/10000", reply);

        var result = await new LinkClient(fixture.Transport).DeleteLinkAsync("10000", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"{LinkPath}/10000", request.PathAndQuery);
        Assert.Null(request.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../10000")]
    [InlineData("1/2")]
    public async Task GivenAMalformedLinkId_WhenDeleting_ThenItIsRefusedWithoutARequest(string id)
    {
        using var fixture = new TransportFixture();

        var result = await new LinkClient(fixture.Transport).DeleteLinkAsync(id, CancellationToken.None);

        Assert.Equal(WriteErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenNoPermissionToDelete_WhenDeleting_ThenPermissionAndNotRepeated()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(HttpStatusCode.Forbidden, "{}");

        fixture.Handler.Serve(HttpMethod.Delete, $"{LinkPath}/10000", reply);

        var result = await new LinkClient(fixture.Transport).DeleteLinkAsync("10000", CancellationToken.None);

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }
}
