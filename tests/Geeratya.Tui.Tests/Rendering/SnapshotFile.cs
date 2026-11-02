using System.Runtime.CompilerServices;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Reads a snapshot, or rewrites it in the source tree when <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> is set.
/// </summary>
internal static class SnapshotFile
{
    private const string UpdateVariable = "GEERATYA_UPDATE_SNAPSHOTS";

    private static bool UpdateRequested
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(UpdateVariable);

            return value is "1" or "true";
        }
    }

    /// <summary>Returns the expected text; in update mode writes <paramref name="actual"/> first and returns it.</summary>
    public static string Expect(string name, string actual, [CallerFilePath] string callerPath = "")
    {
        if (UpdateRequested)
        {
            var sourceDirectory = Path.Combine(Path.GetDirectoryName(callerPath)!, "..", "Snapshots");
            File.WriteAllText(Path.GetFullPath(Path.Combine(sourceDirectory, name)), actual);

            return actual;
        }

        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name));
    }
}
