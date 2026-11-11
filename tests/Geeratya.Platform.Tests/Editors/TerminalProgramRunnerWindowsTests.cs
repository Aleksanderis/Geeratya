using Geeratya.Platform.Editors;

namespace Geeratya.Platform.Tests.Editors;

public sealed class TerminalProgramRunnerWindowsTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("geeratya cmd test ");

    public void Dispose()
    {
        _folder.Delete(true);
    }

    [Fact]
    public async Task GivenACmdScriptInAFolderWithSpaces_WhenRunningIt_ThenItReceivesItsArgumentsAndExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var marker = Path.Combine(_folder.FullName, "marker file.txt");
        var script = Path.Combine(_folder.FullName, "edit.cmd");

        await File.WriteAllTextAsync(script, "@echo off\r\necho %~1>%2\r\nexit /b 3\r\n");

        var exit = await new TerminalProgramRunner().RunAsync(script, ["hello", marker], CancellationToken.None);

        Assert.Equal(3, exit);
        Assert.Equal("hello", (await File.ReadAllTextAsync(marker)).Trim());
    }
}
