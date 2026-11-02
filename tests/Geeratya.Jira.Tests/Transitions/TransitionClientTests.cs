using System.Net;
using System.Text.Json;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transitions;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Transitions;

public sealed class TransitionClientTests
{
    private const string GcmPath = "/rest/api/3/issue/GCM-1/transitions";
    private const string GtmPath = "/rest/api/3/issue/GTM-5/transitions";

    [Fact]
    public async Task GivenCompanyManagedTransitions_WhenListing_ThenTargetsAndCategoriesAreMapped()
    {
        using var fixture = Serving(GcmPath, "transitions/transitions-gcm-1.json");

        var result = await new TransitionClient(fixture.Transport).GetTransitionsAsync("GCM-1", Cancel());

        var transitions = result.Value;
        Assert.Equal(4, transitions.Count);
        var todo = Assert.Single(transitions, transition => transition.Id == "21");
        Assert.Equal("To Do", todo.Name);
        Assert.Equal(new IssueStatus("10004", "To Do", StatusCategoryKeys.Indeterminate), todo.Target);
        Assert.True(todo.IsGlobal);
        Assert.False(todo.HasScreen);
        Assert.Empty(todo.Fields);
        Assert.False(todo.NeedsConfirmation);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal($"{GcmPath}?expand=transitions.fields", request.PathAndQuery);
        Assert.Equal(HttpMethod.Get, request.Method);
    }

    [Fact]
    public async Task GivenTeamManagedTransitions_WhenListing_ThenEveryTransitionIsGlobalWithoutScreen()
    {
        using var fixture = Serving(GtmPath, "transitions/transitions-gtm-5.json");

        var result = await new TransitionClient(fixture.Transport).GetTransitionsAsync("GTM-5", Cancel());

        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, transition => Assert.True(transition.IsGlobal));
        Assert.All(result.Value, transition => Assert.False(transition.NeedsConfirmation));
        Assert.Contains(result.Value, transition => transition.Id == "11" && transition.Name == "To Do");
    }

    [Fact]
    public async Task GivenSyntheticRequiredFieldTransition_WhenListing_ThenFieldMetaCarriesRequiredAndChoices()
    {
        using var fixture = Serving(GcmPath, "writes/synthetic-transitions-required-field.json");

        var result = await new TransitionClient(fixture.Transport).GetTransitionsAsync("GCM-1", Cancel());

        var done = Assert.Single(result.Value);
        Assert.True(done.HasScreen);
        Assert.True(done.NeedsConfirmation);
        Assert.Equal(StatusCategoryKeys.Done, done.Target.CategoryKey);
        var resolution = Assert.Single(done.RequiredFields);
        Assert.Equal("resolution", resolution.FieldId);
        Assert.Equal("Resolution", resolution.Name);
        Assert.Equal("resolution", resolution.SchemaType);
        Assert.Equal(["Done", "Won't Do"], resolution.AllowedValues.Select(option => option.Label));
        Assert.Equal(["10000", "10001"], resolution.AllowedValues.Select(option => option.Id));
    }

    [Fact]
    public async Task GivenUnknownFieldTypeOptionalAndDefaultedFields_WhenListing_ThenTheyAreKeptButNotRequired()
    {
        const string body = """
            {"transitions":[
              {"id":"5","name":"Review","hasScreen":true,"to":{"id":"1","name":"Renamed Status",
               "statusCategory":{"key":"brand-new-category"}},
               "fields":{
                 "customfield_90001":{"required":true,"name":"Sprint goal","schema":{"type":"com.vendor:weird"},
                   "hasDefaultValue":true,"allowedValues":[{"value":"Fast","id":"7"},"junk",{"key":"k"}]},
                 "customfield_90002":{"name":"Notes","schema":{"type":"string"}},
                 "customfield_90003":null,
                 "fixVersions":{"required":true,"fieldId":"fixVersions","name":"Fix versions",
                   "schema":{"type":"array","items":"version"},"allowedValues":[]}}},
              {"id":"6","name":"Hidden","isAvailable":false},
              {"name":"No id"},
              null
            ]}
            """;
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.Ok(body));

        var result = await new TransitionClient(fixture.Transport).GetTransitionsAsync("GCM-1", Cancel());

        var review = Assert.Single(result.Value);
        Assert.Equal("Renamed Status", review.Target.Name);
        Assert.Equal("brand-new-category", review.Target.CategoryKey);
        Assert.Equal(4, review.Fields.Count);
        var weird = review.Fields[0];
        Assert.Equal("com.vendor:weird", weird.SchemaType);
        Assert.Equal(["Fast", "k"], weird.AllowedValues.Select(option => option.Label));
        Assert.Equal("customfield_90003", review.Fields[2].FieldId);
        Assert.Equal(["fixVersions"], review.RequiredFields.Select(meta => meta.FieldId));
    }

    [Fact]
    public async Task GivenEmptyAndMalformedLists_WhenListing_ThenNoTransitionsOrInvalidResponse()
    {
        using var empty = new TransportFixture();
        empty.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.Ok("{}"));
        using var broken = new TransportFixture();
        broken.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.Ok("<html>"));

        var none = await new TransitionClient(empty.Transport).GetTransitionsAsync("GCM-1", Cancel());
        var bad = await new TransitionClient(broken.Transport).GetTransitionsAsync("GCM-1", Cancel());

        Assert.Empty(none.Value);
        Assert.Equal(JiraErrorKind.InvalidResponse, bad.Error.Kind);
    }

    [Theory]
    [InlineData("transitions/transitions-404.json", 404, JiraErrorKind.NotFound)]
    [InlineData("errors/myself-401-bad-token.json", 401, JiraErrorKind.Unauthorized)]
    [InlineData("errors/issue-delete-403-no-permission.json", 403, JiraErrorKind.Forbidden)]
    public async Task GivenErrorReply_WhenListing_ThenTypedError(string fixtureName, int status, JiraErrorKind expected)
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus((HttpStatusCode)status, FixtureFiles.Read(fixtureName));
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, reply);

        var result = await new TransitionClient(fixture.Transport).GetTransitionsAsync("GCM-1", Cancel());

        Assert.Equal(expected, result.Error.Kind);
    }

    [Theory]
    [InlineData("gcm-1")]
    [InlineData("GCM-1/../../x")]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenListingOrPerforming_ThenNothingIsSent(string key)
    {
        using var fixture = new TransportFixture();
        var client = new TransitionClient(fixture.Transport);

        var list = await client.GetTransitionsAsync(key, Cancel());
        var perform = await client.PerformAsync(key, "21", null, null, Cancel());

        Assert.Equal(JiraErrorKind.NotFound, list.Error.Kind);
        Assert.Equal(WriteErrorKind.NotFound, perform.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenBlankTransitionId_WhenPerforming_ThenValidationAndNothingIsSent()
    {
        using var fixture = new TransportFixture();

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", " ", null, null, Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenNoContentReply_WhenPerformingPlainTransition_ThenSuccessAndOnlyIdIsPosted()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", "21", null, null, Cancel());

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(GcmPath, sent.PathAndQuery);
        Assert.Equal("""{"transition":{"id":"21"}}""", sent.Body);
    }

    [Fact]
    public async Task GivenFieldsAndComment_WhenPerforming_ThenBodyCarriesFieldsAndAdfCommentWithUnicode()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        var fields = new Dictionary<string, JsonElement>
        {
            ["resolution"] = Json("""{"id":"10000"}"""),
            ["customfield_90002"] = Json("\"żółć\""),
        };

        var result = await new TransitionClient(fixture.Transport)
            .PerformAsync("GCM-1", "41", fields, "Done 🚀 \"quoted\"", Cancel());

        Assert.True(result.IsSuccess);
        using var sent = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        var root = sent.RootElement;
        Assert.Equal("41", root.GetProperty("transition").GetProperty("id").GetString());
        Assert.Equal("10000", root.GetProperty("fields").GetProperty("resolution").GetProperty("id").GetString());
        Assert.Equal("żółć", root.GetProperty("fields").GetProperty("customfield_90002").GetString());
        var body = root.GetProperty("update").GetProperty("comment")[0].GetProperty("add").GetProperty("body");
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("doc", body.GetProperty("type").GetString());
        var text = body.GetProperty("content")[0].GetProperty("content")[0];
        Assert.Equal("text", text.GetProperty("type").GetString());
        Assert.Equal("Done 🚀 \"quoted\"", text.GetProperty("text").GetString());
    }

    [Fact]
    public async Task GivenRecordedBadTransitionId_WhenPerforming_ThenValidationWithJirasMessageAndNoRetry()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("transitions/transition-400-bad-id.json");
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", "999", null, null, Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Contains("Transition id '999' is not valid for this issue.", result.Error.Messages);
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenSyntheticMissingRequiredField_WhenPerforming_ThenFieldErrorIsKeyedByFieldId()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("writes/synthetic-transition-400-required-field.json");
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", "41", null, null, Cancel());

        var field = Assert.Single(result.Error.FieldErrors);
        Assert.Equal(new WriteFieldError("resolution", "Resolution is required."), field);
    }

    [Fact]
    public async Task GivenConflict_WhenPerforming_ThenConflictAndNotRetried()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/transition-409-conflict.json");
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.Conflict, body));

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", "21", null, null, Cancel());

        Assert.Equal(WriteErrorKind.Conflict, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenLostReply_WhenPerforming_ThenUnknownOutcomeAndReloadAsksForTransitionsOnce()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.Broken());
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.Ok("""{"transitions":[]}"""));
        var client = new TransitionClient(fixture.Transport);

        var write = await client.PerformAsync("GCM-1", "21", null, null, Cancel());
        var reload = await ReadAfterWrite.ReloadIfUnknownAsync(
            write,
            token => client.GetTransitionsAsync("GCM-1", token),
            Cancel());

        Assert.True(write.Error.IsOutcomeUnknown);
        Assert.NotNull(reload);
        Assert.True(reload.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenRateLimitWithoutRetryAfter_WhenPerforming_ThenRateLimitedAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, GcmPath, FakeResponse.WithStatus(HttpStatusCode.TooManyRequests));

        var result = await new TransitionClient(fixture.Transport).PerformAsync("GCM-1", "21", null, null, Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    private static TransportFixture Serving(string path, string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);

        return document.RootElement.Clone();
    }

    private static CancellationToken Cancel() => CancellationToken.None;
}
