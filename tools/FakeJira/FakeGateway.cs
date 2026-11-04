using System.Text;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The tenant lookup and the GraphQL gateway of the fake (#681): the chain that gives a Kanban board's "Hide done work
/// items after", served from the scrubbed dev-site recordings (company-managed board 4, team-managed board 3, Scrum
/// board 6 whose backlog view has no cutoff). Only read queries are answered; a mutation gets 400, as the real client
/// never sends one. The scrubbed recordings carry no real ids, so the chain is matched on the board number.
/// </summary>
public static class FakeGateway
{
    public const string TenantPath = "/_edge/tenant_info";
    public const string GraphQlPath = "/gateway/api/graphql";

    private const string TenantFixture = "gateway/tenant-info.json";
    private const string NavigationMarker = "jira_boardsByIds";
    private const string MutationWord = "mutation";
    private const string NoBoard = """{"data":{"jira_boardsByIds":[null]}}""";
    private const string NoCutoffField = """{"data":{"node":{}}}""";
    private const string Get = "GET";
    private const string Post = "POST";

    /// <summary>The reply for a gateway request, or null when the path is not the gateway's.</summary>
    public static FakeReply? TryRoute(string method, string path, string body, Func<string, byte[]> readFixture)
    {
        if (method == Get && path == TenantPath)
        {
            return new FakeReply(200, readFixture(TenantFixture));
        }

        if (method != Post || path != GraphQlPath)
        {
            return null;
        }

        if (!TryReadQuery(body, out var query, out var variables))
        {
            return new FakeReply(400, Text("""{"errors":[{"message":"Bad request"}]}"""));
        }

        if (query.Contains(MutationWord, StringComparison.OrdinalIgnoreCase))
        {
            return new FakeReply(400, Text("""{"errors":[{"message":"The fake gateway answers queries only"}]}"""));
        }

        return query.Contains(NavigationMarker, StringComparison.Ordinal)
            ? Navigation(variables, readFixture)
            : Cutoff(variables, readFixture);
    }

    private static FakeReply Navigation(string variables, Func<string, byte[]> readFixture)
    {
        return variables switch
        {
            _ when variables.Contains(":board/4\"", StringComparison.Ordinal) => Fixture("navigation-gcm4", readFixture),
            _ when variables.Contains(":board/3\"", StringComparison.Ordinal) => Fixture("navigation-gtm3", readFixture),
            _ when variables.Contains(":board/6\"", StringComparison.Ordinal) => Fixture("navigation-gcs6", readFixture),
            _ => new FakeReply(200, Text(NoBoard)),
        };
    }

    private static FakeReply Cutoff(string variables, Func<string, byte[]> readFixture)
    {
        return variables switch
        {
            _ when variables.Contains("/board/4/board\"", StringComparison.Ordinal) => Fixture("cutoff-gcm4", readFixture),
            _ when variables.Contains("/board/3/board\"", StringComparison.Ordinal) => Fixture("cutoff-gtm3", readFixture),
            _ => new FakeReply(200, Text(NoCutoffField)),
        };
    }

    private static FakeReply Fixture(string name, Func<string, byte[]> readFixture)
    {
        return new FakeReply(200, readFixture($"gateway/{name}.json"));
    }

    private static bool TryReadQuery(string body, out string query, out string variables)
    {
        query = string.Empty;
        variables = string.Empty;

        try
        {
            if (JsonNode.Parse(body) is not JsonObject document || document["query"]?.GetValue<string>() is not { } text)
            {
                return false;
            }

            query = text;
            variables = document["variables"]?.ToJsonString() ?? string.Empty;

            return true;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static byte[] Text(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }
}
