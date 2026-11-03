using System.Text;
using Geeratya.Core.Settings;

namespace Geeratya.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable // Dispose removes the temporary settings folder
{
    private static readonly SettingDefinition _whichKey = SettingsRegistry.Find("whichkey")!;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-store-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_directory, SettingsStore.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenAFileWithCommentsAndUnknownKeys_WhenWriting_ThenOnlyTheValueChanges()
    {
        const string text = "{\n  // my notes\n  \"extra\": [1, 2],\n  \"whichKey\": true,\n}\n";

        Create(text);

        var store = new SettingsStore(_directory);

        store.Read();

        var result = store.Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal(SettingsWriteOutcome.Saved, result.Outcome);
        Assert.Equal(text.Replace("true", "false"), File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenNoFile_WhenWriting_ThenTheFileAndFolderAreCreatedWithOnlyThatKey()
    {
        var store = new SettingsStore(_directory);

        store.Read();
        store.Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal("{\n  \"whichKey\": false\n}\n", File.ReadAllText(FilePath));
        Assert.False(store.Load().WhichKey);
    }

    [Fact]
    public void GivenAMissingKey_WhenWriting_ThenItIsAddedAndTheRestIsKept()
    {
        Create("{\n  // hello\n  \"theme\": \"nord\"\n}\n");

        var store = new SettingsStore(_directory);

        store.Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal("{\n  // hello\n  \"theme\": \"nord\",\n  \"whichKey\": false\n}\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenAFileThatDoesNotParse_WhenWriting_ThenItIsRefusedAndUntouched()
    {
        Create("{ nope");

        var result = new SettingsStore(_directory).Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal(SettingsWriteOutcome.Refused, result.Outcome);
        Assert.Contains("not valid JSON (line 1)", result.Message);
        Assert.Equal("{ nope", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenTheFileChangedAfterReading_WhenWriting_ThenItIsReReadAndNothingIsWritten()
    {
        Create("{ \"theme\": \"a\" }");

        var store = new SettingsStore(_directory);

        store.Read();
        Create("{ \"theme\": \"b\" }");

        var result = store.Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal(SettingsWriteOutcome.Stale, result.Outcome);
        Assert.Equal("b", result.Reloaded!.Theme);
        Assert.Equal("{ \"theme\": \"b\" }", File.ReadAllText(FilePath));
        Assert.Equal(SettingsWriteOutcome.Saved, store.Write(_whichKey, new AppSettings(WhichKey: false)).Outcome);
    }

    [Theory]
    [InlineData("\"minimal\"", HintLevel.Minimal)]
    [InlineData("\"full\"", HintLevel.Full)]
    [InlineData("true", HintLevel.Full)]
    [InlineData("false", HintLevel.Off)]
    public void GivenAStatusHintsValue_WhenReading_ThenItMapsToTheLevel(string json, HintLevel expected)
    {
        Create($"{{ \"statusHints\": {json} }}");

        var load = new SettingsStore(_directory).Read();

        Assert.Equal(expected, load.Settings.StatusHints);
        Assert.Empty(load.Warnings);
    }

    [Fact]
    public void GivenAnInvalidValue_WhenReading_ThenTheDefaultAndAWarningComeBack_AndWritingFixesIt()
    {
        Create("{ \"whichKey\": \"maybe\", \"statusHints\": false, \"unknown\": 1 }");

        var store = new SettingsStore(_directory);
        var load = store.Read();

        Assert.True(load.Settings.WhichKey);
        Assert.Equal(HintLevel.Off, load.Settings.StatusHints);
        Assert.Contains("whichKey must be on or off", Assert.Single(load.Warnings));

        store.Write(_whichKey, load.Settings);

        Assert.Equal(
            "{ \"whichKey\": true, \"statusHints\": false, \"unknown\": 1 }",
            File.ReadAllText(FilePath));
        Assert.Empty(store.Read().Warnings);
    }

    [Fact]
    public void GivenAFileThatDoesNotParse_WhenReading_ThenTheDefaultsComeBackWithAWarning()
    {
        Create("{ nope");

        var load = new SettingsStore(_directory).Read();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("defaults are used", Assert.Single(load.Warnings));
    }

    [Fact]
    public void GivenAByteOrderMark_WhenWriting_ThenItIsKept()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"whichKey\": true }", new UTF8Encoding(true));

        new SettingsStore(_directory).Write(_whichKey, new AppSettings(WhichKey: false));

        var bytes = File.ReadAllBytes(FilePath);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("{ \"whichKey\": false }", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenASymbolicLink_WhenWriting_ThenTheTargetChangesAndTheLinkStays()
    {
        Directory.CreateDirectory(_directory);

        var target = Path.Combine(_directory, "real.json");

        File.WriteAllText(target, "{ \"whichKey\": true }");

        try
        {
            File.CreateSymbolicLink(FilePath, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return; // No permission to create links here (Windows without developer mode)
        }

        new SettingsStore(_directory).Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.NotNull(new FileInfo(FilePath).LinkTarget);
        Assert.Equal("{ \"whichKey\": false }", File.ReadAllText(target));
    }

    private void Create(string text)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, text);
    }
}
