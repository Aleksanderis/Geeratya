using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One snapshot per screen and overlay in the ASCII glyph set. Every character must be ASCII except letters of
/// issue or field text (wide and accented letters, only on the jump and form screens), so a Unicode glyph that leaks in fails here. To refresh after an
/// intended change, run with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class AsciiScreenSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    private static readonly string[] _names =
    [
        "help",
        "whichkey",
        "jump",
        "form",
        "form-picker",
        "editor",
        "contextmenu",
        "profiles",
        "confirm",
        "network-offline",
        "network-ratelimited",
        "network-stale",
        "network-error",
        "network-loading",
    ];

    private static readonly string[] _textScreens = ["jump", "form"];

    public static TheoryData<string, int, int> Screens
    {
        get
        {
            var data = new TheoryData<string, int, int>();

            foreach (var name in _names)
            {
                data.Add(name, 80, 24);
                data.Add(name, 40, 10);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void GivenAScreenInTheAsciiGlyphSet_WhenBuildingTheFrame_ThenItMatchesTheSnapshotAndLeaksNoUnicodeGlyph(
        string name,
        int width,
        int height)
    {
        var lines = FrameBuilder.Build(State(name), width, height, _helpLines, _keymap, NetworkStateSnapshotTests.Now, GlyphSet.Ascii);
        var actual = string.Join('\n', lines) + "\n";

        var expected = SnapshotFile.Expect($"ascii-{name}-{width}x{height}.txt", actual);

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
        Assert.All(lines, line => Assert.All(line, character => Assert.True(
            character < 0x80 || _textScreens.Contains(name) && char.IsLetter(character),
            $"U+{(int)character:X4} leaked into '{line}'")));
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }

    private static AppState State(string name)
    {
        return name switch
        {
            "help" => AppState.Initial with { Mode = InputMode.Help },
            "whichkey" => AppState.Initial with { PendingKeys = "Space" },
            "jump" => JumpOverlaySnapshotTests.Sample("gcm"),
            "form" => FormOverlaySnapshotTests.Sample(),
            "form-picker" => FormOverlaySnapshotTests.WithForm(FormOverlaySnapshotTests.PickerForm()),
            "editor" => CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("hi there\nsecond line")),
            "contextmenu" => ContextMenuOverlayTests.Sample(),
            "profiles" => ProfileListOverlayTests.Sample(),
            "confirm" => ConfirmOverlayTests.Open(),
            _ => NetworkStateSnapshotTests.Sample(name["network-".Length..]),
        };
    }
}
