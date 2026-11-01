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
    public void GivenDefaultKeymap_WhenListingModalOnlyActions_ThenNoneIsBoundInNormalModeAndAllOthersAre()
    {
        var keymap = DefaultKeymap.Load();

        foreach (var definition in ActionCatalog.All)
        {
            var boundInNormal = keymap.FindKeys(InputMode.Normal, definition.Id) is not null;

            Assert.Equal(!ActionCatalog.IsModalOnly(definition.Id), boundInNormal);
        }
    }

    [Fact]
    public void GivenUnimplementedAndModalOnlyLists_WhenChecking_ThenEveryIdIsInTheCatalog()
    {
        foreach (var id in ActionCatalog.Unimplemented.Concat(ActionCatalog.ModalOnly))
        {
            Assert.NotNull(ActionCatalog.FindTitle(id));
        }
    }

    [Fact]
    public void GivenUnimplementedActions_WhenPressingTheirNormalKeys_ThenNothingChanges()
    {
        var keymap = DefaultKeymap.Load();
        var reducer = new AppReducer(keymap);

        foreach (var id in ActionCatalog.Unimplemented)
        {
            var keys = keymap.FindKeys(InputMode.Normal, id)!;
            var state = AppState.Initial;

            foreach (var key in keys.Split(' '))
            {
                state = reducer.Reduce(state, new KeyPressed(key)).State;
            }

            Assert.Equal(AppState.Initial, state);
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

        Assert.Contains(keymap.FindProblems(), problem => problem.Contains("bound twice"));
    }

    [Fact]
    public void GivenSameKeysAndActionTwice_WhenCheckingProblems_ThenReportsTheDuplicate()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "j", ActionIds.NavigateDown),
            new KeyBinding(InputMode.Normal, "j", ActionIds.NavigateDown),
        ]);

        Assert.Contains(keymap.FindProblems(), problem => problem.Contains("bound twice"));
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
