using System.Text;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Jql;

[Trait("Os", "Independent")]
public sealed class JqlBuilderTests
{
    [Theory]
    [InlineData(JqlBuilder.StatusCategoryToDo, "new")]
    [InlineData(JqlBuilder.StatusCategoryInProgress, "indeterminate")]
    [InlineData(JqlBuilder.StatusCategoryDone, "done")]
    public void GivenCategoryConstant_WhenBuildingStatusCategory_ThenKeyNotLocalizedName(string constant, string key)
    {
        Assert.Equal(key, constant);
        Assert.Equal($"statusCategory = \"{key}\"", JqlBuilder.StatusCategory(constant));
    }

    [Theory]
    [InlineData(0, "reporter = currentUser() AND created >= -1m")]
    [InlineData(3, "reporter = currentUser() AND created >= -3m")]
    public void GivenMinutes_WhenBuildingOwnIssuesSince_ThenAtLeastOneMinuteIsUsed(int minutes, string expected)
    {
        Assert.Equal(expected, JqlBuilder.OwnIssuesSince(minutes));
    }

    [Fact]
    public void GivenIssueKey_WhenBuildingParent_ThenVerbatim()
    {
        Assert.Equal("parent = GCM-5", JqlBuilder.Parent("GCM-5"));
    }

    [Fact]
    public void GivenFilterId_WhenBuildingFilter_ThenBareNumber()
    {
        Assert.Equal("filter = 10001", JqlBuilder.Filter("10001"));
    }

    [Fact]
    public void GivenFilterName_WhenBuildingFilter_ThenQuoted()
    {
        Assert.Equal("filter = \"My \\\"best\\\" filter\"", JqlBuilder.Filter("My \"best\" filter"));
    }

    [Fact]
    public void GivenKeys_WhenBuildingKeyIn_ThenVerbatimList()
    {
        Assert.Equal("key in (GCM-1, GTM-22)", JqlBuilder.KeyIn(["GCM-1", "GTM-22"]));
    }

    [Fact]
    public void GivenKeys_WhenBuildingParentIn_ThenVerbatimList()
    {
        Assert.Equal("parent in (GCM-1, GCM-2)", JqlBuilder.ParentIn(["GCM-1", "GCM-2"]));
    }

    [Fact]
    public void GivenNoKeys_WhenBuildingKeyIn_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => JqlBuilder.KeyIn([]));
    }

    [Fact]
    public void GivenSimpleClauses_WhenBuilding_ThenQuotedValues()
    {
        Assert.Equal("project = \"GCM\"", JqlBuilder.Project("GCM"));
        Assert.Equal("issuetype = \"Epic\"", JqlBuilder.IssueTypeEpic());
        var category = JqlBuilder.StatusCategory(JqlBuilder.StatusCategoryInProgress);

        Assert.Equal("statusCategory = \"indeterminate\"", category);
    }

    [Theory]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("trailing\\", "\"trailing\\\\\"")]
    [InlineData("line\nbreak\ttab\r", "\"line\\nbreak\\ttab\\r\"")]
    [InlineData("nul\u0000", "\"nul\\u0000\"")]
    [InlineData("sep\u2028", "\"sep\\u2028\"")]
    [InlineData("emoji \U0001F600 ü 日本", "\"emoji \U0001F600 ü 日本\"")]
    [InlineData("12345", "\"12345\"")]
    [InlineData("IN", "\"IN\"")]
    public void GivenAwkwardValue_WhenQuoting_ThenEscaped(string value, string expected)
    {
        Assert.Equal(expected, JqlBuilder.Quote(value));
    }

    [Theory]
    [InlineData("\"; DROP TABLE issues; --")]
    [InlineData("X\" OR project = \"SECRET")]
    [InlineData("X\\\" OR 1=1 ORDER BY created")]
    [InlineData("X\\")]
    public void GivenHostileProject_WhenBuilding_ThenOnlyOneStringLiteral(string hostile)
    {
        var jql = JqlBuilder.Project(hostile);

        Assert.StartsWith("project = \"", jql);
        Assert.EndsWith("\"", jql);
        Assert.Equal(hostile, Unquote(jql["project = ".Length..]));
    }

    [Theory]
    [InlineData("GCM-1 OR project = X")]
    [InlineData("GCM-1\") OR (\"1")]
    [InlineData("gcm-1")]
    [InlineData("GCM-")]
    [InlineData("-5")]
    [InlineData("12a")]
    public void GivenHostileKey_WhenBuilding_ThenQuoted(string hostile)
    {
        Assert.Equal($"parent = {JqlBuilder.Quote(hostile)}", JqlBuilder.Parent(hostile));
        Assert.Equal($"key in ({JqlBuilder.Quote(hostile)})", JqlBuilder.KeyIn([hostile]));
        Assert.Equal($"filter = {JqlBuilder.Quote(hostile)}", JqlBuilder.Filter(hostile));
    }

    [Fact]
    public void GivenHostileType_WhenBuilding_ThenEscaped()
    {
        Assert.Equal("issuetype = \"a\\\" OR 1=1\"", JqlBuilder.IssueType("a\" OR 1=1"));
        Assert.Equal("statusCategory = \"x\\\\\"", JqlBuilder.StatusCategory("x\\"));
    }

    [Fact]
    public void GivenClauses_WhenCombining_ThenAndSkipsEmptyAndOrIsParenthesised()
    {
        var jql = JqlBuilder.And(
            JqlBuilder.Project("GCM"),
            string.Empty,
            JqlBuilder.Or(JqlBuilder.Parent("GCM-1"), JqlBuilder.Parent("GCM-2")));

        Assert.Equal("project = \"GCM\" AND (parent = GCM-1 OR parent = GCM-2)", jql);
        Assert.Equal(string.Empty, JqlBuilder.Or());
    }

    [Fact]
    public void GivenOrders_WhenAppending_ThenOrderByTerms()
    {
        var jql = JqlBuilder.WithOrder(
            JqlBuilder.Project("GCM"),
            JqlOrder.RankAscending,
            JqlOrder.UpdatedDescending);

        Assert.Equal("project = \"GCM\" ORDER BY Rank ASC, updated DESC", jql);
        Assert.Equal("a ORDER BY created ASC", JqlBuilder.WithOrder("a", JqlOrder.CreatedAscending));
        Assert.Equal("a ORDER BY created DESC", JqlBuilder.WithOrder("a", JqlOrder.CreatedDescending));
        Assert.Equal("a ORDER BY updated ASC", JqlBuilder.WithOrder("a", JqlOrder.UpdatedAscending));
        Assert.Equal("a", JqlBuilder.WithOrder("a"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("ORDER BY created", false)]
    [InlineData("  order   by Rank ASC", false)]
    [InlineData("order\tby created", false)]
    [InlineData("project = GCM", true)]
    [InlineData("project = GCM ORDER BY Rank", true)]
    [InlineData("summary ~ \"order by x\"", true)]
    [InlineData("summary ~ 'order by x'", true)]
    [InlineData("summary ~ 'it\\'s' ORDER BY created", true)]
    [InlineData("summary ~ \"unterminated order by", true)]
    [InlineData("summary ~ \"a\\\" order by\" ORDER BY created", true)]
    [InlineData("reorder by", true)]
    [InlineData("filter = 10001", true)]
    public void GivenJql_WhenCheckingBounds_ThenDetectsMissingRestriction(string? jql, bool expected)
    {
        Assert.Equal(expected, JqlBuilder.IsBounded(jql));
    }

    [Fact]
    public void GivenUnboundedJql_WhenEnsuringBounds_ThenRestrictionComesBeforeOrder()
    {
        var restriction = JqlBuilder.Project("GCM");

        var ordered = JqlBuilder.EnsureBounded("ORDER BY created ASC", restriction);

        Assert.Equal("project = \"GCM\" ORDER BY created ASC", ordered);
        Assert.Equal(restriction, JqlBuilder.EnsureBounded("  ", restriction));
        Assert.Equal("parent = GCM-1", JqlBuilder.EnsureBounded("parent = GCM-1", restriction));
    }

    [Fact]
    public async Task GivenBuiltQuery_WhenSearching_ThenFakeJiraReceivesTheExactJql()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.Ok(FixtureFiles.Read("search/search-empty.json"));
        fixture.Handler.Serve(HttpMethod.Get, JiraSearchClient.SearchPath, reply);
        var jql = JqlBuilder.WithOrder(
            JqlBuilder.And(JqlBuilder.Project("GCM"), JqlBuilder.Parent("GCM-5"), JqlBuilder.Filter("10001")),
            JqlOrder.RankAscending);

        var result = await new JiraSearchClient(fixture.Transport)
            .SearchAsync(new IssueSearchQuery(jql), null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        var received = Uri.UnescapeDataString(request.PathAndQuery.Split("jql=")[1].Split('&')[0]);
        Assert.Equal("project = \"GCM\" AND parent = GCM-5 AND filter = 10001 ORDER BY Rank ASC", received);
    }

    [Theory]
    [InlineData("OR-1")]
    [InlineData("IN-5")]
    [InlineData("AND-3")]
    [InlineData("ORDER-2")]
    public void GivenReservedWordProjectKey_WhenBuilding_ThenKeyStaysBare(string key)
    {
        Assert.Equal($"parent = {key}", JqlBuilder.Parent(key));
        Assert.Equal($"key in ({key}, GCM-1)", JqlBuilder.KeyIn([key, "GCM-1"]));
    }

    /// <summary>Strict: fails when a quote inside the literal is unescaped or an escape dangles at the end.</summary>

    private static string Unquote(string literal)
    {
        Assert.StartsWith("\"", literal);
        Assert.EndsWith("\"", literal);
        var inner = literal[1..^1];
        var result = new StringBuilder();

        for (var index = 0; index < inner.Length; index++)
        {
            Assert.NotEqual('"', inner[index]);

            if (inner[index] == '\\')
            {
                index++;
                Assert.True(index < inner.Length, "Dangling escape would swallow the closing quote.");
            }

            result.Append(inner[index]);
        }

        return result.ToString();
    }
}
