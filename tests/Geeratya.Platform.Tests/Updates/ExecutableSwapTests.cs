using Geeratya.Platform.Directories;
using Geeratya.Platform.Updates;

namespace Geeratya.Platform.Tests.Updates;

public sealed class ExecutableSwapTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("geeratya-swap-");

    public void Dispose()
    {
        _directory.Delete(true);
    }

    [Theory]
    [InlineData("geeratya", "geeratya.new", "geeratya.old")]
    [InlineData("geeratya.exe", "geeratya.new.exe", "geeratya.old.exe")]
    public void GivenAnExecutable_WhenNamingTheSiblings_ThenTheTagGoesBeforeTheExtension(
        string name,
        string staged,
        string old)
    {
        var executable = Path.Combine("folder", name);

        Assert.Equal(Path.Combine("folder", staged), ExecutableSwap.StagedPath(executable));
        Assert.Equal(Path.Combine("folder", old), ExecutableSwap.OldPath(executable));
    }

    [Fact]
    public void GivenALinuxInstall_WhenReplacing_ThenTheNewFileTakesTheNameAndIsExecutable()
    {
        var executable = Write("geeratya", "old");
        var staged = Write("geeratya.new", "new");

        new ExecutableSwap(OperatingSystemKind.Linux).Replace(executable, staged);

        Assert.Equal("new", File.ReadAllText(executable));
        Assert.False(File.Exists(staged));
        Assert.False(File.Exists(ExecutableSwap.OldPath(executable)));

        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(executable).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public void GivenAWindowsInstall_WhenReplacing_ThenTheOldFileMovesAsideAndTheNewOneTakesItsName()
    {
        var executable = Write("geeratya.exe", "old");
        var staged = Write("geeratya.new.exe", "new");

        // A process running the old exe keeps it open; Windows still allows the rename, so hold it open here.
        using var running = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        new ExecutableSwap(OperatingSystemKind.Windows).Replace(executable, staged);

        Assert.Equal("new", File.ReadAllText(executable));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void GivenAWindowsInstall_WhenTheNewFileIsMissing_ThenTheOldBinaryIsRestored()
    {
        var executable = Write("geeratya.exe", "old");

        Assert.ThrowsAny<IOException>(
            () => new ExecutableSwap(OperatingSystemKind.Windows).Replace(
                executable,
                Path.Combine(_directory.FullName, "missing.exe")));

        Assert.Equal("old", File.ReadAllText(executable));
        Assert.False(File.Exists(ExecutableSwap.OldPath(executable)));
    }

    [Fact]
    public void GivenALeftoverOldFile_WhenRemovingOnWindows_ThenItIsDeletedAndOtherSystemsLeaveItAlone()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya");
        var leftover = Write("geeratya.old", "stale");

        new ExecutableSwap(OperatingSystemKind.Linux).RemoveLeftover(executable);

        Assert.True(File.Exists(leftover));

        new ExecutableSwap(OperatingSystemKind.Windows).RemoveLeftover(executable);

        Assert.False(File.Exists(leftover));
    }

    [Fact]
    public void GivenNoLeftover_WhenRemoving_ThenNothingHappens()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya.exe");

        new ExecutableSwap(OperatingSystemKind.Windows).RemoveLeftover(executable);

        Assert.False(File.Exists(ExecutableSwap.OldPath(executable)));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory.FullName, name);

        File.WriteAllText(path, content);

        return path;
    }
}
