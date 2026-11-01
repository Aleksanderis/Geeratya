using Geeratya.Core.Profiles;

namespace Geeratya.Core.Tests.Profiles;

[Trait("Os", "Independent")]
public class ProfileCatalogTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net/", "me@home.test");

    [Fact]
    public void GivenEmptyCatalog_WhenAddingFirstProfile_ThenItBecomesActive()
    {
        var catalog = ProfileCatalog.Empty.With(_work);

        Assert.Equal(_work, catalog.Active);
    }

    [Fact]
    public void GivenTwoProfiles_WhenActivatingSecond_ThenActiveChanges()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_home).Activate("home");

        Assert.Equal(_home, catalog.Active);
    }

    [Fact]
    public void GivenUnknownName_WhenActivating_ThenCatalogIsUnchanged()
    {
        var catalog = ProfileCatalog.Empty.With(_work);

        Assert.Same(catalog, catalog.Activate("nope"));
    }

    [Fact]
    public void GivenActiveProfile_WhenRemoving_ThenNextRemainingBecomesActive()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_home).Without("work");

        Assert.Equal(_home, catalog.Active);
    }

    [Fact]
    public void GivenSameName_WhenAdding_ThenReplacesProfile()
    {
        var changed = _work with { Email = "other@work.test" };

        var catalog = ProfileCatalog.Empty.With(_work).With(changed);

        Assert.Equal([changed], catalog.Profiles);
    }

    [Fact]
    public void GivenTrailingSlashAndCase_WhenReadingCredentialKey_ThenKeyIsNormalised()
    {
        var first = new Profile("a", "https://Site.atlassian.net/", "Me@x.test");
        var second = new Profile("b", "https://site.atlassian.net", "me@x.test");

        Assert.Equal(first.CredentialKey, second.CredentialKey);
    }
}
