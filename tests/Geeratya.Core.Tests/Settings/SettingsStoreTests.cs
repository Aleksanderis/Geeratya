using System.Text;
using Geeratya.Core.Settings;

namespace Geeratya.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable // Dispose removes the temporary settings folder
{
    private static readonly SettingDefinition _whichKey = SettingsRegistry.Find("hints.whichKey")!;

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
        const string text =
            "{\n  // my notes\n  \"extra\": [1, 2],\n  \"hints\": { \"whichKey\": true },\n}\n";

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

        Assert.Equal("{\n  \"hints\": { \"whichKey\": false }\n}\n", File.ReadAllText(FilePath));
        Assert.False(store.Load().WhichKey);
    }

    [Fact]
    public void GivenAMissingKey_WhenWriting_ThenItIsAddedAndTheRestIsKept()
    {
        Create("{\n  // hello\n  \"theme\": \"nord\"\n}\n");

        var store = new SettingsStore(_directory);

        store.Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal(
            "{\n  // hello\n  \"theme\": \"nord\",\n  \"hints\": { \"whichKey\": false }\n}\n",
            File.ReadAllText(FilePath));
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
    public void GivenANestedJumpTarget_WhenReading_ThenItLoads_AndABadValueWarnsAndKeepsTheDefault()
    {
        Create("{ \"jump\": { \"target\": \"tab\" } }");

        Assert.Equal(JumpTarget.Tab, new SettingsStore(_directory).Read().Settings.JumpTarget);

        Create("{ \"jump\": { \"target\": \"here\" } }");

        var bad = new SettingsStore(_directory).Read();

        Assert.Equal(JumpTarget.Current, bad.Settings.JumpTarget);
        Assert.NotEmpty(bad.Warnings);
    }

    [Theory]
    [InlineData("{ \"hints\": 3 }", "hints")]
    [InlineData("{ \"jump\": \"tab\" }", "jump")]
    [InlineData("{ \"startup\": [1] }", "startup")]
    [InlineData("{ \"hints\": null }", "hints")]
    public void GivenAGroupThatIsNotAnObject_WhenReading_ThenOneWarningNamesItAndDefaultsApply(string json, string group)
    {
        Create(json);

        var load = new SettingsStore(_directory).Read();

        Assert.Contains($"settings.json: {group} must be an object", Assert.Single(load.Warnings));
        Assert.Equal(AppSettings.Default, load.Settings);
    }

    [Fact]
    public void GivenAGroupOfObjects_WhenReading_ThenNothingIsReported()
    {
        Create("{ \"hints\": { \"statusBar\": \"off\" }, \"startup\": {} }");

        Assert.Empty(new SettingsStore(_directory).Read().Warnings);
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
            "{ \"whichKey\": \"maybe\", \"statusHints\": false, \"unknown\": 1,\n  \"hints\": { \"whichKey\": true } }",
            File.ReadAllText(FilePath));
        Assert.Empty(store.Read().Warnings);
    }

    [Fact]
    public void GivenOldFlatKeys_WhenReadingAndWriting_ThenTheyLoadAndTheNewKeyWinsAfterTheChange()
    {
        Create("{ \"statusHints\": \"minimal\", \"whichKey\": false }");

        var store = new SettingsStore(_directory);
        var load = store.Read();

        Assert.Equal(HintLevel.Minimal, load.Settings.StatusHints);
        Assert.False(load.Settings.WhichKey);
        Assert.Empty(load.Warnings);

        store.Write(_whichKey, new AppSettings(WhichKey: true));

        var after = store.Read();

        Assert.True(after.Settings.WhichKey);
        Assert.Equal(HintLevel.Minimal, after.Settings.StatusHints);
        Assert.Contains("\"hints\": { \"whichKey\": true }", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenBothTheGroupedAndTheFlatKey_WhenReading_ThenTheGroupedOneWins()
    {
        Create("{ \"whichKey\": false, \"hints\": { \"whichKey\": true } }");

        Assert.True(new SettingsStore(_directory).Read().Settings.WhichKey);
    }

    [Fact]
    public void GivenHintsWithoutTheMember_WhenReading_ThenTheFlatKeySuppliesIt()
    {
        Create("{ \"whichKey\": false, \"hints\": { \"statusBar\": \"minimal\" } }");

        var settings = new SettingsStore(_directory).Read().Settings;

        Assert.False(settings.WhichKey);
        Assert.Equal(HintLevel.Minimal, settings.StatusHints);
    }

    [Theory]
    [InlineData("hints.whichKey")]
    [InlineData("HINTS.WHICHKEY")]
    [InlineData("whichkey")]
    public void GivenANameInAnyCaseOrOldSpelling_WhenFinding_ThenTheSettingIsFound(string name)
    {
        Assert.Equal("hints.whichKey", SettingsRegistry.Find(name)?.Name);
    }

    [Fact]
    public void GivenAHintsParentThatIsNotAnObject_WhenWriting_ThenItIsRefusedAndUntouched()
    {
        Create("{ \"hints\": 3 }");

        var result = new SettingsStore(_directory).Write(_whichKey, new AppSettings(WhichKey: false));

        Assert.Equal(SettingsWriteOutcome.Refused, result.Outcome);
        Assert.Contains("\"hints\"", result.Message);
        Assert.Equal("{ \"hints\": 3 }", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenAnInvalidNestedValue_WhenReading_ThenTheWarningNamesTheNewKey()
    {
        Create("{ \"hints\": { \"statusBar\": 7 } }");

        var load = new SettingsStore(_directory).Read();

        Assert.Contains("hints.statusBar must be full, minimal or off", Assert.Single(load.Warnings));
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
        File.WriteAllText(FilePath, "{ \"hints\": { \"whichKey\": true } }", new UTF8Encoding(true));

        new SettingsStore(_directory).Write(_whichKey, new AppSettings(WhichKey: false));

        var bytes = File.ReadAllBytes(FilePath);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("{ \"hints\": { \"whichKey\": false } }", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GivenASymbolicLink_WhenWriting_ThenTheTargetChangesAndTheLinkStays()
    {
        Directory.CreateDirectory(_directory);

        var target = Path.Combine(_directory, "real.json");

        File.WriteAllText(target, "{ \"hints\": { \"whichKey\": true } }");

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
        Assert.Equal("{ \"hints\": { \"whichKey\": false } }", File.ReadAllText(target));
    }

    private void Create(string text)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, text);
    }
}
