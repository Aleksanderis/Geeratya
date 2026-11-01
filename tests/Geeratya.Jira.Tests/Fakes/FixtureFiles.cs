namespace Geeratya.Jira.Tests.Fakes;

/// <summary>Reads recorded fixtures copied next to the test assembly.</summary>
public static class FixtureFiles
{
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));
}
