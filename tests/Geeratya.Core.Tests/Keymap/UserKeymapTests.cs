using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Keymap;

public sealed class UserKeymapTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-keymap-{Guid.NewGuid():N}");

    public UserKeymapTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void GivenNoFile_WhenMerging_ThenDefaultsAndNoProblems()
    {
        var defaults = DefaultKeymap.Load().Bindings;

        var result = UserKeymap.Merge(defaults, null);

        Assert.Equal(defaults, result.Bindings);
        Assert.Empty(result.Problems);
        Assert.Null(result.Notice);
    }

    [Fact]
    public void GivenOverride_WhenMerging_ThenNewKeysRunTheActionAndOtherDefaultsStay()
    {
        var table = Merge("""{ "bindings": [ { "mode": "normal", "keys": "Ctrl-j", "action": "nav.down" } ] }""");

        Assert.Equal(ActionIds.NavigateDown, table.Resolve(InputMode.Normal, string.Empty, "Ctrl-j").Action);
        Assert.Equal(ActionIds.NavigateDown, table.Resolve(InputMode.Normal, string.Empty, "j").Action);
        Assert.Equal("Ctrl-j", table.FindKeys(InputMode.Normal, ActionIds.NavigateDown));
        Assert.Empty(table.FindProblems());
    }

    [Fact]
    public void GivenRowWithDefaultKeys_WhenMerging_ThenItReplacesTheDefaultAction()
    {
        var table = Merge("""{ "bindings": [ { "mode": "normal", "keys": "q", "action": "help.toggle" } ] }""");

        Assert.Equal(ActionIds.HelpToggle, table.Resolve(InputMode.Normal, string.Empty, "q").Action);
        Assert.Empty(table.FindProblems());
    }

    [Fact]
    public void GivenUnbind_WhenMerging_ThenTheKeyDoesNothing()
    {
        var table = Merge("""{ "unbind": [ { "mode": "normal", "keys": "q" } ] }""");

        Assert.Equal(KeyResolutionKind.None, table.Resolve(InputMode.Normal, string.Empty, "q").Kind);
    }

    [Fact]
    public void GivenUnbindOfUnknownKeys_WhenMerging_ThenProblemIsReported()
    {
        var table = Merge("""{ "unbind": [ { "mode": "normal", "keys": "Ctrl-q" } ] }""", out var problems);

        Assert.Contains("unbind normal Ctrl-q", Assert.Single(problems));
        Assert.NotEmpty(table.Bindings);
    }

    [Fact]
    public void GivenUnknownAction_WhenMerging_ThenRowIsSkippedAndReported()
    {
        Merge("""{ "bindings": [ { "mode": "normal", "keys": "Ctrl-j", "action": "nav.sideways" } ] }""", out var problems);

        Assert.Contains("unknown action nav.sideways", Assert.Single(problems));
    }

    [Fact]
    public void GivenBadKeySpelling_WhenMerging_ThenRowIsSkippedAndReported()
    {
        var table = Merge(
            """{ "bindings": [ { "mode": "normal", "keys": "Control-j", "action": "nav.down" } ] }""",
            out var problems);

        Assert.Contains("bad key spelling 'Control-j'", Assert.Single(problems));
        Assert.Equal(KeyResolutionKind.None, table.Resolve(InputMode.Normal, string.Empty, "Control-j").Kind);
    }

    [Fact]
    public void GivenSequenceShadowingDefault_WhenMerging_ThenConflictIsReportedAndDefaultKept()
    {
        var table = Merge(
            """{ "bindings": [ { "mode": "normal", "keys": "g", "action": "nav.down" } ] }""",
            out var problems);

        Assert.Contains("would shadow g g", Assert.Single(problems));
        Assert.Equal(KeyResolutionKind.Prefix, table.Resolve(InputMode.Normal, string.Empty, "g").Kind);
    }

    [Fact]
    public void GivenSequenceShadowingDefaultAndUnbind_WhenMerging_ThenTheNewBindingWins()
    {
        var table = Merge(
            """
            { "unbind": [
                { "mode": "normal", "keys": "g g" },
                { "mode": "normal", "keys": "g t" },
                { "mode": "normal", "keys": "g T" } ],
              "bindings": [ { "mode": "normal", "keys": "g", "action": "nav.top" } ] }
            """,
            out var problems);

        Assert.Empty(problems);
        Assert.Equal(ActionIds.NavigateTop, table.Resolve(InputMode.Normal, string.Empty, "g").Action);
    }

    [Fact]
    public void GivenTwoRowsWithSameKeys_WhenMerging_ThenSecondIsReported()
    {
        var table = Merge(
            """
            { "bindings": [
                { "mode": "normal", "keys": "Ctrl-j", "action": "nav.down" },
                { "mode": "normal", "keys": "Ctrl-j", "action": "nav.up" } ] }
            """,
            out var problems);

        Assert.Contains("already bound to nav.down", Assert.Single(problems));
        Assert.Equal(ActionIds.NavigateDown, table.Resolve(InputMode.Normal, string.Empty, "Ctrl-j").Action);
    }

    [Fact]
    public void GivenPrintableKeyInInsertMode_WhenMerging_ThenRowIsRejected()
    {
        Merge("""{ "bindings": [ { "mode": "insert", "keys": "x", "action": "form.submit" } ] }""", out var problems);

        Assert.Contains("printable keys are text in Insert mode", Assert.Single(problems));
    }

    [Fact]
    public void GivenModalOnlyActionInNormalMode_WhenMerging_ThenRowIsRejected()
    {
        Merge("""{ "bindings": [ { "mode": "normal", "keys": "Ctrl-y", "action": "confirm.yes" } ] }""", out var problems);

        Assert.Contains("cannot be bound in Normal mode", Assert.Single(problems));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "bindings": [ { "mode": "sideways", "keys": "x", "action": "nav.down" } ] }""")]
    [InlineData("null")]
    public void GivenCorruptFile_WhenMerging_ThenDefaultsStayAndOneProblemIsReported(string json)
    {
        var defaults = DefaultKeymap.Load().Bindings;

        var result = UserKeymap.Merge(defaults, json);

        Assert.Equal(defaults, result.Bindings);
        Assert.Contains("defaults are in use", Assert.Single(result.Problems));
        Assert.StartsWith("keymap.json:", result.Notice);
    }

    [Fact]
    public void GivenCommentsAndTrailingCommas_WhenMerging_ThenTheFileIsAccepted()
    {
        var table = Merge(
            """
            {
              // my keys
              "bindings": [ { "mode": "normal", "keys": "Ctrl-j", "action": "nav.down" }, ],
            }
            """,
            out var problems);

        Assert.Empty(problems);
        Assert.Equal(ActionIds.NavigateDown, table.Resolve(InputMode.Normal, string.Empty, "Ctrl-j").Action);
    }

    [Fact]
    public void GivenRowsWithoutMode_WhenMerging_ThenBothAreReportedAndNothingChanges()
    {
        var table = Merge(
            """
            { "bindings": [ { "keys": "x", "action": "app.quit" } ],
              "unbind": [ { "keys": "q" } ] }
            """,
            out var problems);

        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Contains("needs", problem));
        Assert.Equal(ActionIds.Quit, table.Resolve(InputMode.Normal, string.Empty, "q").Action);
        Assert.Equal(KeyResolutionKind.None, table.Resolve(InputMode.Normal, string.Empty, "x").Kind);
    }

    [Fact]
    public void GivenCtrlKeyInInsertMode_WhenMerging_ThenItIsAccepted()
    {
        Merge("""{ "bindings": [ { "mode": "insert", "keys": "Ctrl-g", "action": "form.submit" } ] }""", out var problems);

        Assert.Empty(problems);
    }

    [Fact]
    public void GivenSpaceInCommandMode_WhenMerging_ThenItIsRejected()
    {
        Merge("""{ "bindings": [ { "mode": "command", "keys": "Space", "action": "palette.submit" } ] }""", out var problems);

        Assert.Contains("printable keys are text", Assert.Single(problems));
    }

    [Fact]
    public void GivenSeveralProblems_WhenBuildingNotice_ThenItNamesTheFirstAndCountsTheRest()
    {
        var result = new KeymapLoadResult([], ["first", "second", "third"]);

        Assert.Equal("keymap.json: first (and 2 more; see the Messages overlay)", result.Notice);
    }

    [Fact]
    public void GivenStoreWithoutFile_WhenLoading_ThenDefaults()
    {
        var defaults = DefaultKeymap.Load().Bindings;

        var result = new UserKeymapStore(_directory).Load(defaults);

        Assert.Equal(defaults, result.Bindings);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void GivenFileOnDisk_WhenLoadingAndReloadingAfterEdit_ThenReducerFollowsTheFile()
    {
        var defaults = DefaultKeymap.Load();
        var table = DefaultKeymap.Load();
        var reducer = new AppReducer(table);
        var store = new UserKeymapStore(_directory);
        var path = Path.Combine(_directory, UserKeymapStore.FileName);

        File.WriteAllText(path, """{ "bindings": [ { "mode": "normal", "keys": "Ctrl-j", "action": "help.toggle" } ] }""");
        table.Replace(store.Load(defaults.Bindings).Bindings);

        var first = reducer.Reduce(AppState.Initial, new KeyPressed("Ctrl-j"));

        Assert.Equal(InputMode.Help, first.State.Mode);

        File.WriteAllText(path, "{}");
        table.Replace(store.Load(defaults.Bindings).Bindings);

        var second = reducer.Reduce(AppState.Initial, new KeyPressed("Ctrl-j"));

        Assert.Equal(InputMode.Normal, second.State.Mode);
    }

    [Fact]
    public void GivenKeysReloadCommand_WhenSubmittingPalette_ThenReloadEffect()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = "keys reload" };

        var result = reducer.Reduce(state, new KeyPressed("Enter"));

        Assert.Single(result.Effects.OfType<ReloadKeymapEffect>());
    }

    [Fact]
    public void GivenKeysWithoutReload_WhenSubmittingPalette_ThenUsageAndNoEffect()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = "keys" };

        var result = reducer.Reduce(state, new KeyPressed("Enter"));

        Assert.Empty(result.Effects);
        Assert.Equal("Usage: :keys reload", result.State.Message);
    }

    [Fact]
    public void GivenKeymapReloaded_WhenReducing_ThenMessageIsShown()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());

        var result = reducer.Reduce(AppState.Initial, new KeymapReloaded("Keymap reloaded"));

        Assert.Equal("Keymap reloaded", result.State.Message);
    }

    [Fact]
    public void GivenSeveralKeymapProblems_WhenReloading_ThenEachIsAWarningInTheHistory()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var result = new KeymapLoadResult([], ["first", "second", "third"]);

        var state = reducer.Reduce(AppState.Initial, new KeymapReloaded(result.Notice!, result.Problems)).State;

        Assert.Equal(StatusSeverity.Warning, state.Status!.Severity);
        Assert.Equal(["third", "second", "first"], state.StatusHistory.Skip(1).Select(message => message.Text));
        Assert.All(state.StatusHistory.Skip(1), message => Assert.Equal(StatusSeverity.Warning, message.Severity));
    }

    [Fact]
    public void GivenOneKeymapProblem_WhenReloading_ThenItIsOneWarningNotTwo()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var result = new KeymapLoadResult([], ["only"]);

        var state = reducer.Reduce(AppState.Initial, new KeymapReloaded(result.Notice!, result.Problems)).State;

        Assert.Single(state.StatusHistory);
        Assert.Equal(StatusSeverity.Warning, state.Status!.Severity);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("help")]
    [InlineData("command")]
    [InlineData("confirm")]
    public void GivenFileUnbindingOrRebindingCtrlC_WhenMerging_ThenBothRowsAreRefusedAndCtrlCStillQuits(string mode)
    {
        var json = $$"""
            { "unbind": [ { "mode": "{{mode}}", "keys": "Ctrl-c" } ],
              "bindings": [ { "mode": "{{mode}}", "keys": "Ctrl-c", "action": "nav.down" } ] }
            """;

        var table = Merge(json, out var problems);

        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Contains("always quits", problem));
        Assert.Equal(ActionIds.Quit, table.Resolve(Enum.Parse<InputMode>(mode, ignoreCase: true), string.Empty, "Ctrl-c").Action);
    }

    [Fact]
    public void GivenFileUnbindingQuitEscAndColon_WhenPressingCtrlC_ThenTheAppStillQuits()
    {
        var json = """
            { "unbind": [ { "mode": "normal", "keys": "q" }, { "mode": "normal", "keys": "Esc" },
                          { "mode": "normal", "keys": ":" }, { "mode": "normal", "keys": "Ctrl-c" } ] }
            """;
        var result = UserKeymap.Merge(DefaultKeymap.Load().Bindings, json);
        var table = new KeymapTable(result.Bindings.Where(binding => binding.Keys != "Ctrl-c").ToList());
        var reducer = new AppReducer(table);

        var quit = reducer.Reduce(AppState.Initial, new KeyPressed("Ctrl-c"));
        var plainQ = reducer.Reduce(AppState.Initial, new KeyPressed("q"));

        Assert.Single(quit.Effects.OfType<QuitEffect>());
        Assert.Empty(plainQ.Effects);
    }

    private static KeymapTable Merge(string json)
    {
        return Merge(json, out _);
    }

    private static KeymapTable Merge(string json, out IReadOnlyList<string> problems)
    {
        var result = UserKeymap.Merge(DefaultKeymap.Load().Bindings, json);

        problems = result.Problems;

        return new KeymapTable(result.Bindings);
    }
}
