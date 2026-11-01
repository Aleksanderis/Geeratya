using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class KeymapTableTests
{
    [Fact]
    public void GivenDefaultKeymap_WhenCheckingProblems_ThenThereAreNone()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Empty(keymap.FindProblems());
    }

    [Fact]
    public void GivenDefaultKeymap_WhenListingCatalogActions_ThenEveryActionIsBound()
    {
        var keymap = DefaultKeymap.Load();
        var bound = keymap.Bindings.Select(binding => binding.Action).ToHashSet();

        foreach (var definition in ActionCatalog.All)
        {
            Assert.Contains(definition.Id, bound);
        }
    }

    [Fact]
    public void GivenSameKeysTwiceInOneMode_WhenCheckingProblems_ThenReportsTheConflict()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "j", ActionIds.NavigateDown),
            new KeyBinding(InputMode.Normal, "j", ActionIds.NavigateUp),
        ]);

        Assert.Contains(keymap.FindProblems(), problem => problem.Contains("bound to both"));
    }

    [Fact]
    public void GivenSameKeysInDifferentModes_WhenCheckingProblems_ThenThereAreNone()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "j", ActionIds.NavigateDown),
            new KeyBinding(InputMode.Help, "j", ActionIds.NavigateDown),
        ]);

        Assert.Empty(keymap.FindProblems());
    }

    [Fact]
    public void GivenSequenceShadowedByShorterBinding_WhenCheckingProblems_ThenReportsIt()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "g", ActionIds.NavigateTop),
            new KeyBinding(InputMode.Normal, "g g", ActionIds.NavigateTop),
        ]);

        Assert.Contains(keymap.FindProblems(), problem => problem.Contains("shadows"));
    }

    [Fact]
    public void GivenUnknownActionAndBadSpelling_WhenCheckingProblems_ThenReportsBoth()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "c-d", "no.such")]);

        var problems = keymap.FindProblems();

        Assert.Contains(problems, problem => problem.Contains("unknown action"));
        Assert.Contains(problems, problem => problem.Contains("bad key spelling"));
    }

    [Fact]
    public void GivenFirstKeyOfSequence_WhenResolving_ThenIsPrefixAndSecondKeyCompletes()
    {
        var keymap = DefaultKeymap.Load();

        var first = keymap.Resolve(InputMode.Normal, string.Empty, "g");
        var second = keymap.Resolve(InputMode.Normal, "g", "g");

        Assert.Equal(KeyResolutionKind.Prefix, first.Kind);
        Assert.Equal(KeyResolutionKind.Action, second.Kind);
        Assert.Equal(ActionIds.NavigateTop, second.Action);
    }

    [Fact]
    public void GivenUnboundKey_WhenResolving_ThenNone()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Equal(KeyResolutionKind.None, keymap.Resolve(InputMode.Normal, string.Empty, "z").Kind);
        Assert.Equal(KeyResolutionKind.None, keymap.Resolve(InputMode.Normal, "g", "x").Kind);
    }
}
