using System.Text.RegularExpressions;
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
    public void GivenTheReference_WhenRenderingMarkdown_ThenEveryRowNamesItsLevel()
    {
        var markdown = KeymapDocument.RenderMarkdown(DefaultKeymap.Load());

        Assert.Contains("| Keys | Action | Id | Level |", markdown);
        Assert.Contains("| `Space i d` | Delete the issue (asks first; Jira cannot undo it) | `issue.delete` | advanced |", markdown);
        Assert.Contains("| `Space i s` | Change the status of the issue | `issue.transition` | everyday |", markdown);
    }

    [Fact]
    public void GivenTheReference_WhenRenderingMarkdown_ThenEveryBindingRowEndsWithItsActionId()
    {
        var keymap = DefaultKeymap.Load();
        var markdown = KeymapDocument.RenderMarkdown(keymap);

        foreach (var binding in keymap.Bindings)
        {
            var pattern = $@"\| `{Regex.Escape(binding.Keys)}` \| [^|]+ \| `{Regex.Escape(binding.Action)}` \| (everyday|advanced) \|";

            Assert.Matches(pattern, markdown);
        }

        var keyRows = markdown.Split("## Palette commands")[0].Split('\n').Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .ToList();

        Assert.All(keyRows, row => Assert.Matches(@"\| `[^`]+` \| (everyday|advanced) \|$", row));
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
