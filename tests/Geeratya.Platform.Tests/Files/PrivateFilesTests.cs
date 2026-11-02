using Geeratya.Platform.Directories;
using Geeratya.Platform.Files;

namespace Geeratya.Platform.Tests.Files;

public sealed class PrivateFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"geeratya-private-{Guid.NewGuid():N}");

    [Fact]
    public void GivenUnix_WhenCreatingNestedFolderAndRestrictingFile_ThenOnlyTheUserHasAccess()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var files = new PrivateFiles(OperatingSystemKind.Linux);
        var folder = Path.Combine(_root, "a", "b");
        var file = Path.Combine(folder, "x.json");

        files.CreateDirectory(folder);
        File.WriteAllText(file, "{}");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        files.RestrictFile(file);

        var folderMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Assert.Equal(folderMode, File.GetUnixFileMode(folder));
        Assert.Equal(folderMode, File.GetUnixFileMode(Path.Combine(_root, "a")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public void GivenWindows_WhenCreatingAFolder_ThenItExistsAndRestrictingAFileChangesNothing()
    {
        var files = new PrivateFiles(OperatingSystemKind.Windows);
        var folder = Path.Combine(_root, "win");
        var file = Path.Combine(folder, "x.json");

        files.CreateDirectory(folder);
        File.WriteAllText(file, "{}");
        files.RestrictFile(file);

        Assert.True(File.Exists(file));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
