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

        var lines = KeymapDocument.RenderHelpLines(keymap);

        foreach (var binding in keymap.Bindings)
        {
            var title = ActionCatalog.FindTitle(binding.Action)!;

            Assert.Contains(lines, line => line.Contains(binding.Keys) && line.Contains(title));
        }
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
