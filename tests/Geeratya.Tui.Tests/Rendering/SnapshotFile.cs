namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Reads a snapshot, or rewrites it in the source tree when <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> is set.
/// </summary>
internal static class SnapshotFile
{
    private const string UpdateVariable = "GEERATYA_UPDATE_SNAPSHOTS";

    private static bool UpdateRequested => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>Returns the expected text (the actual text is written exactly as given); in update mode writes <paramref name="actual"/> first and returns it.</summary>
    public static string Expect(string name, string actual)
    {
        if (UpdateRequested)
        {
            File.WriteAllText(SourcePath(name), actual);

            return actual;
        }

        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name));
    }

    private static string SourcePath(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Geeratya.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "Geeratya.Tui.Tests", "Snapshots", file);
    }
}
