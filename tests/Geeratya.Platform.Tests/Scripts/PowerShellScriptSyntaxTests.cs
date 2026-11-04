using System.Diagnostics;

namespace Geeratya.Platform.Tests.Scripts;

public sealed class PowerShellScriptSyntaxTests
{
    private const string ParseCommand =
        "$failed = 0; foreach ($file in $args) { $errors = $null; " +
        "[System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$null, [ref]$errors) | Out-Null; " +
        "foreach ($problem in $errors) { Write-Output \"$file : $($problem.Message)\"; $failed++ } }; exit $failed";

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

        foreach (var script in scripts)
        {
            start.ArgumentList.Add(script);
        }

        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, output);
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
