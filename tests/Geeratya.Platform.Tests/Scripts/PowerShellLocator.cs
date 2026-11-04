namespace Geeratya.Platform.Tests.Scripts;

/// <summary>Finds the <c>pwsh</c> executable on the PATH.</summary>
public static class PowerShellLocator
{
    public static string? Find()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var name = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
