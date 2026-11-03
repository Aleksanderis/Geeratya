using Geeratya.Core.Keymap;

namespace Geeratya.Core.Tests.Keymap;

public class KeymapDocumentTests
{
    private const string SolutionFile = "Geeratya.slnx";

    [Fact]
    public void GivenDefaultKeymap_WhenRenderingMarkdown_ThenMatchesCommittedKeyReference()
    {
        var expected = KeymapDocument.RenderMarkdown(DefaultKeymap.Load());
        var path = Path.Combine(FindRepositoryRoot(), "docs", "keymap.md");

        var actual = File.ReadAllText(path).Replace("\r\n", "\n");

        Assert.True(expected == actual, "docs/keymap.md is stale: run scripts/gen-keymap-doc.sh");
    }

    [Fact]
    public void GivenDefaultKeymap_WhenRenderingHelp_ThenEveryBindingAppearsWithItsTitle()
    {
        var keymap = DefaultKeymap.Load();

        var lines = KeymapDocument.RenderHelpLines(keymap, CommandLevel.Advanced);

        foreach (var binding in keymap.Bindings)
        {
            var title = ActionCatalog.FindTitle(binding.Action)!;

            Assert.Contains(lines, line => line.Contains(binding.Keys) && line.Contains(title));
        }
    }

    [Fact]
    public void GivenEverydayLevel_WhenRenderingHelp_ThenAdvancedBindingsAreLeftOutAndCounted()
    {
        var keymap = DefaultKeymap.Load();

        var lines = KeymapDocument.RenderHelpLines(keymap);

        Assert.DoesNotContain(lines, line => line.Contains(ActionCatalog.FindTitle(ActionIds.IssueDelete)!));
        Assert.Contains(lines, line => line.Contains(ActionCatalog.FindTitle(ActionIds.IssueTransition)!));
        Assert.Contains(lines, line => line.Contains("advanced commands hidden; Space * shows them"));
    }

    [Fact]
    public void GivenAdvancedLevel_WhenRenderingHelp_ThenNothingIsHiddenAndNoNoteShows()
    {
        var lines = KeymapDocument.RenderHelpLines(DefaultKeymap.Load(), CommandLevel.Advanced);

        Assert.DoesNotContain(lines, line => line.Contains("hidden"));
    }

    [Fact]
    public void GivenTheCatalog_WhenCheckingCompleteness_ThenEveryActionHasATitleAGroupAndALevelAndShowsInHelp()
    {
        var keymap = DefaultKeymap.Load();
        var lines = KeymapDocument.RenderHelpLines(keymap, CommandLevel.Advanced);

        foreach (var definition in ActionCatalog.All.Where(definition => !ActionCatalog.IsModalOnly(definition.Id)))
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title), $"{definition.Id} has no description");
            Assert.True(Enum.IsDefined(definition.Level), $"{definition.Id} has no level");

            Assert.True(
                keymap.Bindings.Any(binding => binding.Action == definition.Id),
                $"{definition.Id} is bound to no key, so help cannot list it");
        }

        foreach (var binding in keymap.Bindings)
        {
            var title = ActionCatalog.FindTitle(binding.Action)!;

            Assert.Contains(lines, line => line.Contains(binding.Keys) && line.Contains(title));
        }
    }

    [Fact]
    public void GivenTheReference_WhenRenderingMarkdown_ThenEveryRowNamesItsLevel()
    {
        var markdown = KeymapDocument.RenderMarkdown(DefaultKeymap.Load());

        Assert.Contains("| Keys | Action | Level |", markdown);
        Assert.Contains("| `Space i d` | Delete the issue (asks first; Jira cannot undo it) | advanced |", markdown);
        Assert.Contains("| `Space i s` | Change the status of the issue | everyday |", markdown);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenCheckingEveryPrefix_ThenSomethingIsBoundBelowIt()
    {
        var keymap = DefaultKeymap.Load();
        var prefixes = keymap.Bindings
            .Select(binding => (binding.Mode, Parts: binding.Keys.Split(' ')))
            .Where(entry => entry.Parts.Length > 1)
            .SelectMany(entry => Enumerable.Range(1, entry.Parts.Length - 1)
                .Select(length => (entry.Mode, Prefix: string.Join(' ', entry.Parts.Take(length)))))
            .Distinct()
            .ToList();

        Assert.NotEmpty(prefixes);

        foreach (var (mode, prefix) in prefixes)
        {
            Assert.NotEmpty(keymap.NextKeys(mode, prefix));
        }

        Assert.NotEmpty(keymap.NextKeys(InputMode.Normal, "Space", CommandLevel.Everyday));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"{SolutionFile} not found.");
    }
}
