using Geeratya.Core.Storage;
using Geeratya.Core.Tests.Fakes;

namespace Geeratya.Core.Tests.Storage;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-atomic-{Guid.NewGuid():N}");

    public AtomicFileTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void GivenAnExistingFile_WhenWriting_ThenItIsReplacedAndNoTemporaryFileRemains()
    {
        var path = Path.Combine(_directory, "state.json");
        File.WriteAllText(path, "old");

        new AtomicFile().WriteAllText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void GivenTheTargetIsLockedTwice_WhenWriting_ThenTheReplaceIsRetriedWithGrowingPauses()
    {
        var path = Path.Combine(_directory, "state.json");
        var replacer = new LockedFileReplacer(_ => true, failures: 2);

        new AtomicFile(replacer).WriteAllText(path, "content");

        Assert.Equal("content", File.ReadAllText(path));
        Assert.Equal(3, replacer.Attempts);
        Assert.Equal([TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40)], replacer.Pauses);
    }

    [Fact]
    public void GivenAnUnauthorizedAccessOnReplace_WhenWriting_ThenItIsRetriedToo()
    {
        var path = Path.Combine(_directory, "state.json");
        var replacer = new LockedFileReplacer(_ => true, failures: 1, new UnauthorizedAccessException());

        new AtomicFile(replacer).WriteAllText(path, "content");

        Assert.Equal("content", File.ReadAllText(path));
        Assert.Single(replacer.Pauses);
    }

    [Fact]
    public void GivenTheTargetStaysLocked_WhenWriting_ThenTheLastErrorIsThrownAndTheTemporaryFileIsRemoved()
    {
        var path = Path.Combine(_directory, "state.json");
        File.WriteAllText(path, "old");
        var replacer = new LockedFileReplacer(_ => true, failures: int.MaxValue);

        Assert.Throws<IOException>(() => new AtomicFile(replacer).WriteAllText(path, "new"));

        Assert.Equal(5, replacer.Attempts);
        Assert.Equal(4, replacer.Pauses.Count);
        Assert.Equal("old", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void GivenAMissingFolder_WhenWriting_ThenItFailsAtOnceWithoutRetrying()
    {
        var replacer = new LockedFileReplacer(_ => false, failures: 0);
        var path = Path.Combine(_directory, "missing", "state.json");

        Assert.ThrowsAny<IOException>(() => new AtomicFile(replacer).WriteAllText(path, "x"));

        Assert.Empty(replacer.Pauses);
    }

    [Fact]
    public void GivenTwoWrites_WhenWriting_ThenEachUsesItsOwnTemporaryName()
    {
        var path = Path.Combine(_directory, "state.json");
        var replacer = new LockedFileReplacer(_ => false, failures: 0);
        var file = new AtomicFile(replacer);

        file.WriteAllText(path, "a");
        file.WriteAllText(path, "b");

        Assert.Equal(2, replacer.Temporaries.Distinct().Count());
        Assert.All(replacer.Temporaries, name => Assert.Contains($".{Environment.ProcessId}.", name));
        Assert.All(replacer.Temporaries, name => Assert.EndsWith(".tmp", name, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFailingWriteCallback_WhenWriting_ThenTheTargetIsUntouchedAndNoTemporaryFileRemains()
    {
        var path = Path.Combine(_directory, "state.json");
        File.WriteAllText(path, "old");

        Assert.Throws<InvalidOperationException>(
            () => new AtomicFile().Write(path, _ => throw new InvalidOperationException()));

        Assert.Equal("old", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void GivenAReaderIsOpen_WhenWriting_ThenTheTargetIsStillReplaced()
    {
        var path = Path.Combine(_directory, "state.json");
        File.WriteAllText(path, "old");

        using (AtomicFile.OpenRead(path))
        {
            new AtomicFile().WriteAllText(path, "new");
        }

        Assert.Equal("new", AtomicFile.ReadAllText(path));
    }

    [Fact]
    public void GivenAWriterHoldsTheFileOpen_WhenReading_ThenItIsReadWithoutASharingViolation()
    {
        var path = Path.Combine(_directory, "state.json");

        using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            writer.Write("held"u8);
            writer.Flush();

            Assert.Equal("held", AtomicFile.ReadAllText(path));
        }
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
