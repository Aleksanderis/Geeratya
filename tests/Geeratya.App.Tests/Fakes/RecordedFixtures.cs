namespace Geeratya.App.Tests.Fakes;

/// <summary>The recorded Jira fixtures of the Jira layer tests, copied next to the test assembly.</summary>
public static class RecordedFixtures
{
    /// <summary>The text of a fixture, for example <c>issues/issue-gcm24.json</c>.</summary>
    public static string Read(string relativePath)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));
    }
}
