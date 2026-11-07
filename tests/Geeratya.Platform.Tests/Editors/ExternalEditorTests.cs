using Geeratya.Platform.Directories;
using Geeratya.Platform.Editors;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Tests.Editors;

public sealed class ExternalEditorTests : IDisposable
{
    private const string Wide = "世界 👍 héllo";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("geeratya-editor-");
    private readonly FakeTerminalProgramRunner _runner = new();

    public void Dispose()
    {
        _folder.Delete(true);
    }

    [Fact]
    public async Task GivenEditedText_WhenEditing_ThenWideCharactersAndNewlinesRoundTripAndTheFileIsGone()
    {
        _runner.Replacement = "line one 世界\nline two 👍\n\nend\n";
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        var result = await editor.EditAsync(Wide, CancellationToken.None);

        Assert.Equal("line one 世界\nline two 👍\n\nend\n", result.Text);
        Assert.Null(result.Error);
        Assert.Equal(Wide, _runner.SeenText);
        Assert.Equal("vim", _runner.Program);
        Assert.False(File.Exists(_runner.Path));
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task GivenTheSameTextWithAFinalNewline_WhenEditing_ThenItCountsAsUnchanged()
    {
        _runner.Replacement = "one\ntwo\n";
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        var result = await editor.EditAsync("one\ntwo", CancellationToken.None);

        Assert.Equal(ExternalEditResult.Unchanged, result);
    }

    [Fact]
    public async Task GivenAnEmptiedFile_WhenEditing_ThenTheEmptyTextIsReturned()
    {
        _runner.Replacement = string.Empty;
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        var result = await editor.EditAsync("text", CancellationToken.None);

        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public async Task GivenBothVariables_WhenEditing_ThenVisualWinsAndItsArgumentsComeFirst()
    {
        var editor = Create(OperatingSystemKind.Linux, ("VISUAL", "code --wait"), ("EDITOR", "nano"));

        await editor.EditAsync("x", CancellationToken.None);

        Assert.Equal("code", _runner.Program);
        Assert.Equal("--wait", _runner.Arguments[0]);
        Assert.Equal(2, _runner.Arguments.Count);
    }

    [Fact]
    public async Task GivenALinuxEditor_WhenEditing_ThenTheFileIsPrivateFromTheStartAndInTheGivenFolder()
    {
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        await editor.EditAsync("secret", CancellationToken.None);

        Assert.Equal(_folder.FullName, Path.GetDirectoryName(_runner.Path));

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, _runner.SeenMode);
        }
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    public async Task GivenNoVariableOnUnix_WhenEditing_ThenItFailsNamingTheVariablesAndStartsNothing(
        OperatingSystemKind system)
    {
        var editor = Create(system);

        var result = await editor.EditAsync("x", CancellationToken.None);

        Assert.Contains("$VISUAL or $EDITOR", result.Error, StringComparison.Ordinal);
        Assert.Null(result.Text);
        Assert.Null(_runner.Program);
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task GivenNoVariableOnWindows_WhenEditing_ThenNotepadRuns()
    {
        var editor = Create(OperatingSystemKind.Windows);

        await editor.EditAsync("x", CancellationToken.None);

        Assert.Equal("notepad", _runner.Program);
    }

    [Fact]
    public async Task GivenANonZeroExit_WhenEditing_ThenItFailsEvenIfTheFileChanged()
    {
        _runner.Replacement = "changed";
        _runner.ExitCode = 1;
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Null(result.Text);
        Assert.Contains("exited with code 1", result.Error, StringComparison.Ordinal);
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task GivenAMissingProgram_WhenEditing_ThenItFailsNamingItAndRemovesTheFile()
    {
        _runner.Failure = new ProgramNotFoundException("nope", new InvalidOperationException());
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "nope"));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Contains("'nope' was not found", result.Error, StringComparison.Ordinal);
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task GivenACancelledRun_WhenEditing_ThenTheCancellationPropagatesAndTheFileIsRemoved()
    {
        _runner.Failure = new OperationCanceledException();
        var editor = Create(OperatingSystemKind.Linux, ("EDITOR", "vim"));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => editor.EditAsync("old", CancellationToken.None));

        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task GivenAFolderThatDoesNotExist_WhenEditing_ThenItFailsWithoutStartingTheEditor()
    {
        var editor = new ExternalEditor(
            OperatingSystemKind.Linux,
            Environment("EDITOR", "vim"),
            _runner,
            Path.Combine(_folder.FullName, "missing"));

        var result = await editor.EditAsync("x", CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Null(_runner.Program);
    }

    [Fact]
    public async Task GivenTheUnsupportedEditor_WhenEditing_ThenItAlwaysFails()
    {
        var result = await new UnsupportedExternalEditor().EditAsync("x", CancellationToken.None);

        Assert.NotNull(result.Error);
    }

    private ExternalEditor Create(OperatingSystemKind system, params (string Name, string Value)[] variables)
    {
        return new ExternalEditor(system, Environment(variables), _runner, _folder.FullName);
    }

    private static EnvironmentReader Environment(params (string Name, string Value)[] variables)
    {
        return new EnvironmentReader(name => variables.FirstOrDefault(pair => pair.Name == name).Value);
    }

    private static EnvironmentReader Environment(string name, string value)
    {
        return Environment((name, value));
    }
}
