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
    [InlineData("geeratya", "geeratya.old")]
    [InlineData("geeratya.exe", "geeratya.old.exe")]
    public void GivenAnExecutable_WhenNamingTheSiblings_ThenTheTagGoesBeforeTheExtension(
        string name,
        string old)
    {
        var executable = Path.Combine("folder", name);

        Assert.Equal(Path.Combine("folder", old), ExecutableSwap.OldPath(executable));
    }

    [Fact]
    public void GivenAnExecutable_WhenStaging_ThenTheNameIsUniqueToTheProcess()
    {
        var staged = ExecutableSwap.StagedPath(Path.Combine("folder", "geeratya.exe"));

        Assert.Equal(Path.Combine("folder", $"geeratya.new.{Environment.ProcessId}.exe"), staged);
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
    public void GivenAnOldFileThatCannotBeDeleted_WhenReplacingOnWindows_ThenTheUpdateStillSucceeds()
    {
        var executable = Write("geeratya.exe", "old");
        var staged = Write("geeratya.new.exe", "new");

        // A directory cannot be deleted with File.Delete, which stands in for a copy still running from the old name.
        var locked = ExecutableSwap.OldPath(executable);

        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "keep"), "x");

        new ExecutableSwap(OperatingSystemKind.Windows).Replace(executable, staged);

        Assert.Equal("new", File.ReadAllText(executable));
        Assert.False(File.Exists(staged));
        Assert.Empty(Directory.GetFiles(_directory.FullName, "geeratya.old.*.exe"));

        // The parked binary was deleted at once; only the locked name remains (the sweep of parked files is covered below).
        Assert.True(Directory.Exists(locked));
    }

    [Fact]
    public void GivenAParkedOldFile_WhenRemovingOnWindows_ThenEveryOldNameAndTheScriptNameAreDeleted()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya.exe");
        var leftovers = new[]
        {
            Write("geeratya.old.exe", "a"),
            Write("geeratya.old.1.exe", "b"),
            Write("geeratya.old.2.exe", "c"),
            Write("geeratya.exe.old", "script"),
        };
        var other = Write("geeratya.exe.config", "keep");

        new ExecutableSwap(OperatingSystemKind.Windows).RemoveLeftover(executable);

        Assert.All(leftovers, leftover => Assert.False(File.Exists(leftover)));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void GivenAStaleStagedFile_WhenRemovingOnWindows_ThenItIsDeletedButNotThisProcessesOrAFreshOne()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya.exe");
        var stale = DateTime.UtcNow - ExecutableSwap.StaleStagedAge - TimeSpan.FromMinutes(1);
        var killed = Write("geeratya.new.4242.exe", "killed");
        var own = Write(Path.GetFileName(ExecutableSwap.StagedPath(executable)), "staging");
        var running = Write("geeratya.new.4343.exe", "another update, still going");
        var other = Write("geeratya.newer.txt", "keep");

        File.SetLastWriteTimeUtc(killed, stale);
        File.SetLastWriteTimeUtc(own, stale);
        new ExecutableSwap(OperatingSystemKind.Windows).RemoveLeftover(executable);

        Assert.False(File.Exists(killed));
        Assert.True(File.Exists(own));
        Assert.True(File.Exists(running));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void GivenAStaleStagedFile_WhenRemovingOnLinux_ThenItIsLeftAlone()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya");
        var killed = Write("geeratya.new.4242", "killed");

        File.SetLastWriteTimeUtc(killed, DateTime.UtcNow - ExecutableSwap.StaleStagedAge - TimeSpan.FromMinutes(1));
        new ExecutableSwap(OperatingSystemKind.Linux).RemoveLeftover(executable);

        Assert.True(File.Exists(killed));
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
