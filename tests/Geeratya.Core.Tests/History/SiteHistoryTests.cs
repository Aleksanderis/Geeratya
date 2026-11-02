using Geeratya.Core.History;

namespace Geeratya.Core.Tests.History;

[Trait("Os", "Independent")]
public class SiteHistoryTests
{
    [Fact]
    public void GivenQueries_WhenAdded_ThenTheNewestIsFirst()
    {
        var history = SiteHistory.Empty.WithQuery("project = A").WithQuery("project = B");

        Assert.Equal(["project = B", "project = A"], history.Queries);
    }

    [Fact]
    public void GivenAnExistingQuery_WhenAddedAgain_ThenItMovesToTheFrontWithoutDuplicate()
    {
        var history = SiteHistory.Empty.WithQuery("a = 1").WithQuery("b = 2").WithQuery("  a = 1  ");

        Assert.Equal(["a = 1", "b = 2"], history.Queries);
    }

    [Fact]
    public void GivenMoreQueriesThanTheCap_WhenAdded_ThenTheOldestFallOff()
    {
        var history = SiteHistory.Empty;

        for (var index = 0; index < SiteHistory.MaxQueries + 5; index++)
        {
            history = history.WithQuery($"id = {index}");
        }

        Assert.Equal(SiteHistory.MaxQueries, history.Queries.Count);
        Assert.Equal($"id = {SiteHistory.MaxQueries + 4}", history.Queries[0]);
        Assert.DoesNotContain("id = 0", history.Queries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenBlankQuery_WhenAdded_ThenNothingChanges(string query)
    {
        Assert.Same(SiteHistory.Empty, SiteHistory.Empty.WithQuery(query));
    }

    [Fact]
    public void GivenAnOverlongQuery_WhenAdded_ThenItIsNotRemembered()
    {
        var history = SiteHistory.Empty.WithQuery(new string('x', SiteHistory.MaxQueryLength + 1));

        Assert.Empty(history.Queries);
    }

    [Fact]
    public void GivenIssueKeys_WhenAddedInAnyCase_ThenTheyAreUpperCasedAndDeduplicated()
    {
        var history = SiteHistory.Empty.WithIssue("gcm-1").WithIssue("GCM-2").WithIssue("Gcm-1");

        Assert.Equal(["GCM-1", "GCM-2"], history.Issues);
    }

    [Fact]
    public void GivenMoreIssuesThanTheCap_WhenAdded_ThenTheOldestFallOff()
    {
        var history = SiteHistory.Empty;

        for (var index = 0; index < SiteHistory.MaxIssues + 3; index++)
        {
            history = history.WithIssue($"A-{index}");
        }

        Assert.Equal(SiteHistory.MaxIssues, history.Issues.Count);
        Assert.DoesNotContain("A-0", history.Issues);
    }

    [Fact]
    public void GivenAHandEditedFile_WhenNormalized_ThenDuplicatesAndBlanksGoAndOrderStays()
    {
        var raw = new SiteHistory(["a = 1", "", "b = 2", "a = 1"], ["x-1", "X-1", " "]);

        var history = raw.Normalized();

        Assert.Equal(["a = 1", "b = 2"], history.Queries);
        Assert.Equal(["X-1"], history.Issues);
    }
}
