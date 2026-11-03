using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The columns layout of the recorded company-managed and team-managed boards at the three snapshot sizes. To
/// refresh after an intended change, copy the actual frame from the failure into the matching file and review the
/// diff.
/// </summary>
public class ColumnsSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData("gcm", 120, 36)]
    [InlineData("gcm", 80, 24)]
    [InlineData("gcm", 40, 10)]
    [InlineData("gtm", 120, 36)]
    [InlineData("gtm", 80, 24)]
    [InlineData("gtm", 40, 10)]
    public async Task GivenALoadedBoard_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(
        string kind,
        int width,
        int height)
    {
        var state = await OpenAsync(kind, height);

        var lines = FrameBuilder.Build(state, width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var name = $"columns-{kind}-{width}x{height}.txt";
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name));

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    [Theory]
    [InlineData("gcm", 120, 36)]
    [InlineData("gcm", 80, 24)]
    [InlineData("gtm", 80, 24)]
    public async Task GivenTheAsciiGlyphSet_WhenBuildingTheBoardFrame_ThenItMatchesTheSnapshotAndDrawsOnlyAscii(
        string kind,
        int width,
        int height)
    {
        var state = await OpenAsync(kind, height);

        var lines = FrameBuilder.Build(state, width, height, _keymap, null, GlyphSet.Ascii);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"columns-ascii-{kind}-{width}x{height}.txt", actual);

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
        Assert.All(lines, line => Assert.DoesNotContain(line, character => Glyphs.AsciiSwapped.Contains(character)));
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }

    [Theory]
    [InlineData("gcm", "Backlog", "To Do", "In Progress", "Done")]
    [InlineData("gtm", "To Do", "In Progress", "Backlog", "Done")]
    public async Task GivenEachBoardKind_WhenBuilding_ThenColumnsAreInBoardOrderAndOneCardIsMarked(
        string kind,
        string first,
        string second,
        string third,
        string fourth)
    {
        var state = await OpenAsync(kind, 36);

        var lines = FrameBuilder.Build(state, 120, 36, _keymap);

        var positions = new[] { first, second, third, fourth }
            .Select(name => lines[3].IndexOf(name, StringComparison.Ordinal))
            .ToArray();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Contains(lines.Skip(4), line => line.Contains('┃', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("gcm")]
    [InlineData("gtm")]
    public async Task GivenEachBoardKind_WhenBuildingAtEverySize_ThenEveryLineKeepsTheWidth(string kind)
    {
        var state = await OpenAsync(kind, 20);

        for (var width = 1; width <= 130; width += 3)
        {
            for (var height = 1; height <= 40; height += 3)
            {
                var lines = FrameBuilder.Build(state, width, height, _keymap);

                Assert.Equal(height, lines.Count);
                Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
            }
        }
    }

    private static Task<AppState> OpenAsync(string kind, int height)
    {
        var rows = height - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows;

        return kind == "gcm" ? BoardStateFixtures.CompanyManagedAsync(rows) : BoardStateFixtures.TeamManagedAsync(rows);
    }
}
