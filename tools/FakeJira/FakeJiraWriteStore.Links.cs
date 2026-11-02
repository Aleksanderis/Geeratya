using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The issue link part of the write store: <c>POST /issueLink</c> and <c>DELETE /issueLink/{id}</c>. A new link is
/// listed on both issues the way Jira lists it (only the other issue, as <c>outwardIssue</c> or <c>inwardIssue</c>;
/// the issue posted as <c>inwardIssue</c> reads the outward wording, "blocks");
/// a removed link disappears from both. A recorded link (an id below <see cref="FirstLinkId"/>) is removed by id only:
/// the fake cannot tell whether such an id exists, so any id not removed before is accepted.
/// </summary>
public sealed partial class FakeJiraWriteStore
{
    private const int FirstLinkId = 40000;
    private const string LinkTypesFixture = "links/issuelink-types.json";
    private const string IssueLinksName = "issuelinks";
    private const string InwardIssueName = "inwardIssue";
    private const string OutwardIssueName = "outwardIssue";
    private const string InwardName = "inward";
    private const string OutwardName = "outward";
    private const string TypeName = "type";
    private const string IssueLinkTypesName = "issueLinkTypes";

    private readonly HashSet<string> _removedLinkIds = [];
    private int _nextLinkId = FirstLinkId;

    public FakeReply CreateLink(string body)
    {
        var node = ParseObject(body);
        var typeId = StringOf(node?[TypeName]?[IdName]);
        var inwardKey = StringOf(node?[InwardIssueName]?[KeyName]);
        var outwardKey = StringOf(node?[OutwardIssueName]?[KeyName]);

        if (typeId is null || inwardKey is null || outwardKey is null || inwardKey == outwardKey)
        {
            return Reply(BadRequest, MissingFieldsFixture);
        }

        var type = LinkTypes().FirstOrDefault(candidate => StringOf(candidate?[IdName]) == typeId);

        lock (_lock)
        {
            if (type is null)
            {
                return Reply(BadRequest, MissingFieldsFixture);
            }

            if (Find(outwardKey) is not { } outward || Find(inwardKey) is not { } inward)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            var id = (_nextLinkId++).ToString(CultureInfo.InvariantCulture);
            var name = StringOf(type[NameName]) ?? string.Empty;
            var inwardText = StringOf(type[InwardName]) ?? name;
            var outwardText = StringOf(type[OutwardName]) ?? name;

            // The issue posted as inwardIssue reads the outward wording ("blocks"), as Jira does.
            inward.Links.Add(new StoredLink(id, typeId, name, inwardText, outwardText, outwardKey, true));
            outward.Links.Add(new StoredLink(id, typeId, name, inwardText, outwardText, inwardKey, false));

            return Empty(Created);
        }
    }

    public FakeReply DeleteLink(string id)
    {
        lock (_lock)
        {
            var found = false;

            foreach (var stored in _issues.Values)
            {
                found |= stored.Links.RemoveAll(link => link.Id == id) > 0;
            }

            var recorded = int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number < FirstLinkId;

            if (found || (recorded && _removedLinkIds.Add(id)))
            {
                return Empty(NoContent);
            }

            return Reply(NotFound, NotFoundFixture);
        }
    }

    /// <summary>The recorded issue without the links removed through the fake; null when nothing was removed.</summary>
    private byte[]? WithoutRemovedLinks(byte[]? recorded)
    {
        if (recorded is null || _removedLinkIds.Count == 0)
        {
            return null;
        }

        var node = JsonNode.Parse(recorded)!;
        var before = (node[FieldsName]?[IssueLinksName] as JsonArray)?.Count ?? 0;

        ApplyLinks(node, null);

        var after = (node[FieldsName]?[IssueLinksName] as JsonArray)?.Count ?? 0;

        return before == after ? null : Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    private void ApplyLinks(JsonNode node, StoredIssue? stored)
    {
        if (node[FieldsName] is not JsonObject fields)
        {
            return;
        }

        var existing = fields[IssueLinksName] as JsonArray ?? [];
        var kept = new List<JsonNode?>();

        foreach (var entry in existing)
        {
            if (!_removedLinkIds.Contains(StringOf(entry?[IdName]) ?? string.Empty))
            {
                kept.Add(entry?.DeepClone());
            }
        }

        foreach (var link in stored?.Links ?? [])
        {
            kept.Add(LinkNode(link));
        }

        if (kept.Count > 0 || fields.ContainsKey(IssueLinksName))
        {
            fields[IssueLinksName] = new JsonArray([.. kept]);
        }
    }

    private static JsonObject LinkNode(StoredLink link)
    {
        var other = new JsonObject
        {
            [KeyName] = link.OtherKey,
            [FieldsName] = new JsonObject { [SummaryName] = link.OtherKey },
        };

        return new JsonObject
        {
            [IdName] = link.Id,
            [TypeName] = new JsonObject
            {
                [IdName] = link.TypeId,
                [NameName] = link.TypeName,
                [InwardName] = link.Inward,
                [OutwardName] = link.Outward,
            },
            [link.ReadsOutward ? OutwardIssueName : InwardIssueName] = other,
        };
    }

    private List<JsonNode?> LinkTypes()
    {
        var types = JsonNode.Parse(_readFixture(LinkTypesFixture))?[IssueLinkTypesName] as JsonArray;

        return [.. types ?? []];
    }

    private static JsonObject? ParseObject(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
