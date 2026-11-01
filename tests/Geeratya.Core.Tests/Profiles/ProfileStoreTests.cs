using Geeratya.Core.Profiles;

namespace Geeratya.Core.Tests.Profiles;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenNoFile_WhenLoading_ThenEmptyCatalogAndMissing()
    {
        var result = new ProfileStore(_directory).Load();

        Assert.Equal(ProfileLoadStatus.Missing, result.Status);
        Assert.Empty(result.Catalog.Profiles);
    }

    [Fact]
    public void GivenSavedCatalog_WhenLoading_ThenRoundTripsWithActive()
    {
        var store = new ProfileStore(Path.Combine(_directory, "nested"));
        var catalog = ProfileCatalog.Empty
            .With(new Profile("work", "https://work.atlassian.net", "me@work.test"))
            .With(new Profile("home", "https://home.atlassian.net", "me@home.test"))
            .Activate("home");

        store.Save(catalog);
        var result = store.Load();

        Assert.Equal(ProfileLoadStatus.Loaded, result.Status);
        Assert.Equal("home", result.Catalog.ActiveName);
        Assert.Equal(catalog.Profiles, result.Catalog.Profiles);
    }

    [Theory]
    [InlineData("not json {")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"profiles\":[null]}")]
    [InlineData("{\"profiles\":[{\"name\":\"a\"}]}")]
    [InlineData("{\"profiles\":[{\"name\":\"a\",\"siteUrl\":\"\",\"email\":\"\"}]}")]
    public void GivenCorruptFile_WhenLoading_ThenEmptyCatalogAndCopyKept(string content)
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, ProfileStore.FileName);
        File.WriteAllText(file, content);

        var result = new ProfileStore(_directory).Load();

        Assert.Equal(ProfileLoadStatus.Corrupt, result.Status);
        Assert.Empty(result.Catalog.Profiles);
        Assert.Equal(content, File.ReadAllText($"{file}.corrupt"));
    }

    [Fact]
    public void GivenSavedCatalog_WhenReadingFile_ThenContainsNoTokenField()
    {
        var store = new ProfileStore(_directory);

        store.Save(ProfileCatalog.Empty.With(new Profile("work", "https://w.atlassian.net", "me@w.test")));
        var text = File.ReadAllText(Path.Combine(_directory, ProfileStore.FileName));

        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credentialKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"active\"", text);
    }
}
