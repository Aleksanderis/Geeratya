namespace Geeratya.Tui.Tests.Fakes;

/// <summary>Reads the recorded board fixtures copied next to the test assembly.</summary>
public static class BoardFixtureFiles
{
    public static string Read(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "boards", $"{name}.json"));
    }
}
