using System.Diagnostics;

namespace Geeratya.Platform.Tests.Scripts;

public sealed class PowerShellScriptSyntaxTests
{
    private const string FilesVariable = "GEERATYA_PS_FILES";

    private const string ParseCommand =
        "$failed = 0; foreach ($file in ($env:GEERATYA_PS_FILES -split \"`n\")) { $errors = $null; " +
        "[System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$null, [ref]$errors) | Out-Null; " +
        "foreach ($problem in $errors) { Write-Output \"$file : $($problem.Message)\"; $failed = 1 } }; exit $failed";

    [PowerShellAvailableFact]
    public async Task GivenThePowerShellScripts_WhenParsed_ThenThereAreNoSyntaxErrors()
    {
        var scripts = Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "scripts"), "*.ps1");
        Assert.NotEmpty(scripts);
        var start = new ProcessStartInfo(PowerShellLocator.Find()!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(ParseCommand);
        start.Environment[FilesVariable] = string.Join('\n', scripts);

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"{await output}{await error}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("global.json not found.");
    }
}
