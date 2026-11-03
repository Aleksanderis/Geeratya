using Geeratya.Core.Settings;

namespace Geeratya.Core.Tests.Settings;

public sealed class SettingsDocumentTests
{
    [Fact]
    public void GivenTheRegistry_WhenReadingTheDocument_ThenItsTableIsTheGeneratedOne()
    {
        var document = File.ReadAllText(DocumentPath());

        Assert.Contains(SettingsRegistry.DocumentationTable(), document.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void GivenTheRegistry_WhenListingNames_ThenNamesAndKeysAreUnique()
    {
        var names = SettingsRegistry.All.Select(setting => setting.Name);
        var keys = SettingsRegistry.All.Select(setting => setting.ConfigKey);

        Assert.Equal(SettingsRegistry.All.Count, names.Distinct().Count());
        Assert.Equal(SettingsRegistry.All.Count, keys.Distinct().Count());
    }

    [Fact]
    public void GivenEverySetting_WhenApplyingItsDefault_ThenTheSettingsDoNotChange()
    {
        foreach (var setting in SettingsRegistry.All)
        {
            Assert.Equal(setting.Default, setting.Read(AppSettings.Default));
            Assert.Equal(AppSettings.Default, setting.Apply(AppSettings.Default, setting.Default));
        }
    }

    private static string DocumentPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "settings.md")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new FileNotFoundException("docs/settings.md"), "docs", "settings.md");
    }
}
