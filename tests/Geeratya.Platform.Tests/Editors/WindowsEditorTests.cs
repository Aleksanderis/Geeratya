using Geeratya.Platform.Directories;
using Geeratya.Platform.Editors;

namespace Geeratya.Platform.Tests.Editors;

public sealed class WindowsEditorTests : IDisposable
{
    private const string VisualCode = @"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("geeratya-winedit-");
    private readonly FakeTerminalProgramRunner _runner = new();

    public void Dispose()
    {
        _folder.Delete(true);
    }

    [Fact]
    public void GivenCodeCmdOnPath_WhenResolvingCode_ThenTheCmdFileIsFound()
    {
        var resolver = Resolver([$@"{VisualCode}\code.cmd"], VisualCode, ".com;.exe;.bat;.cmd");

        Assert.Equal($@"{VisualCode}\code.cmd", resolver.Resolve("code"));
    }

    [Fact]
    public void GivenSeveralFolders_WhenResolving_ThenTheFirstFolderAndTheFirstExtensionWin()
    {
        var resolver = Resolver(
            [@"C:\b\ed.cmd", @"C:\b\ed.exe", @"C:\c\ed.exe"],
            @"C:\a;C:\b;C:\c",
            ".exe;.cmd");

        Assert.Equal(@"C:\b\ed.exe", resolver.Resolve("ed"));
    }

    [Fact]
    public void GivenAnExtensionInTheName_WhenResolving_ThenTheNameIsTriedAsGiven()
    {
        var resolver = Resolver([@"C:\b\code.cmd"], @"C:\b", ".EXE;.CMD");

        Assert.Equal(@"C:\b\code.cmd", resolver.Resolve("code.cmd"));
    }

    [Fact]
    public void GivenAFullPathWithoutExtension_WhenResolving_ThenOnlyThatFolderIsSearched()
    {
        var resolver = Resolver([@"C:\tools\ed.bat", @"C:\other\ed.bat"], @"C:\other", ".exe;.bat");

        Assert.Equal(@"C:\tools\ed.bat", resolver.Resolve(@"C:\tools\ed"));
    }

    [Fact]
    public void GivenNothingMatches_WhenResolving_ThenTheNameIsReturnedUnchanged()
    {
        var resolver = Resolver([], @"C:\a", ".EXE");

        Assert.Equal("nope", resolver.Resolve("nope"));
    }

    [Fact]
    public void GivenNoPathext_WhenResolving_ThenTheWindowsDefaultsApply()
    {
        var resolver = Resolver([@"C:\a\ed.CMD"], @"C:\a", null);

        Assert.Equal(@"C:\a\ed.CMD", resolver.Resolve("ed"));
    }

    [Theory]
    [InlineData("code.cmd", true)]
    [InlineData("RUN.BAT", true)]
    [InlineData("code.exe", false)]
    [InlineData("code", false)]
    public void GivenAProgram_WhenAskingForABatchFile_ThenOnlyCmdAndBatCount(string program, bool expected)
    {
        Assert.Equal(expected, WindowsProgramResolver.IsBatchFile(program));
    }

    [Fact]
    public void GivenAScriptWithSpaces_WhenBuildingTheCmdLine_ThenEveryWordIsQuotedInsideTheOuterPair()
    {
        var line = CmdCommandLine.Build(@"C:\Program Files\Code\code.cmd", ["--wait", @"C:\Temp\a b.md"]);

        Assert.Equal(
            "/d /s /c \"\"C:\\Program Files\\Code\\code.cmd\" \"--wait\" \"C:\\Temp\\a b.md\"\"",
            line);
    }

    [Theory]
    [InlineData("a\"b")]
    [InlineData("%PATH%")]
    [InlineData("a\nb")]
    public void GivenAnUnsafeWord_WhenBuildingTheCmdLine_ThenItIsRefused(string word)
    {
        Assert.Throws<NotSupportedException>(() => CmdCommandLine.Build("code.cmd", [word]));
    }

    [Fact]
    public async Task GivenEditorCodeOnWindows_WhenEditing_ThenTheResolvedCmdPathRuns()
    {
        _runner.Replacement = "new text";
        var probe = new FakeFileProbe($@"{VisualCode}\code.cmd");
        var editor = Create(probe, ("EDITOR", "code --wait"), ("PATH", VisualCode), ("PATHEXT", ".exe;.cmd"));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Equal("new text", result.Text);
        Assert.Equal($@"{VisualCode}\code.cmd", _runner.Program);
        Assert.Equal("--wait", _runner.Arguments[0]);
    }

    [Fact]
    public async Task GivenAnEditorThatReturnsAtOnceWithoutChange_WhenEditingOnWindows_ThenTheResultSaysToWait()
    {
        var editor = Create(
            new FakeFileProbe($@"{VisualCode}\code.cmd"),
            new SteppingTimeProvider(TimeSpan.FromMilliseconds(10)),
            ("EDITOR", "code"),
            ("PATH", VisualCode));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Null(result.Text);
        Assert.Contains("--wait", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABatchEditorThatTakesLongWithoutChange_WhenEditingOnWindows_ThenItIsJustUnchanged()
    {
        var editor = Create(
            new FakeFileProbe($@"{VisualCode}\code.cmd"),
            new SteppingTimeProvider(TimeSpan.FromSeconds(30)),
            ("EDITOR", "code"),
            ("PATH", VisualCode));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Null(result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GivenAnExecutableEditorThatQuitsAtOnceWithoutChange_WhenEditingOnWindows_ThenItIsJustUnchanged()
    {
        var editor = Create(new FakeFileProbe(@"C:\vim\vim.exe"), ("EDITOR", "vim"), ("PATH", @"C:\vim"));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Null(result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GivenAnUnsafeEditorCommand_WhenEditing_ThenItFailsWithoutLeavingTheFile()
    {
        _runner.Failure = new NotSupportedException();
        var editor = Create(new FakeFileProbe(), ("EDITOR", "code.cmd"));

        var result = await editor.EditAsync("old", CancellationToken.None);

        Assert.Contains("cannot be started", result.Error, StringComparison.Ordinal);
        Assert.Empty(_folder.GetFiles());
    }

    private static WindowsProgramResolver Resolver(string[] files, string path, string? pathext)
    {
        var environment = new EnvironmentReader(name => name switch
        {
            "PATH" => path,
            "PATHEXT" => pathext,
            _ => null,
        });

        return new WindowsProgramResolver(environment, new FakeFileProbe(files));
    }

    private ExternalEditor Create(FakeFileProbe probe, params (string Name, string Value)[] variables)
    {
        return Create(probe, new SteppingTimeProvider(TimeSpan.FromMilliseconds(10)), variables);
    }

    private ExternalEditor Create(
        FakeFileProbe probe,
        TimeProvider time,
        params (string Name, string Value)[] variables)
    {
        var environment = new EnvironmentReader(name => variables.FirstOrDefault(pair => pair.Name == name).Value);

        return new ExternalEditor(OperatingSystemKind.Windows, environment, _runner, _folder.FullName, probe, time);
    }
}
