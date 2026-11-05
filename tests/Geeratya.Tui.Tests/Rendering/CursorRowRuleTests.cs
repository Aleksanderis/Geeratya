using System.Text.RegularExpressions;

namespace Geeratya.Tui.Tests.Rendering;

public class CursorRowRuleTests
{
    private static readonly Regex _mark = new(@"^[│|]?\s?>\S?\s?\S", RegexOptions.Compiled);

    [Fact]
    public void GivenEverySnapshot_WhenReadingItsLines_ThenNoRowStartsWithACursorMarkExceptTheInputPrompts()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Snapshots");
        var files = Directory.GetFiles(directory, "*.txt")
            .Where(file => !Path.GetFileName(file).Contains("jump", StringComparison.Ordinal)
                && !Path.GetFileName(file).Contains("picker", StringComparison.Ordinal)
                && !Path.GetFileName(file).Contains("prompt", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(files);

        var offenders = files
            .SelectMany(file => File.ReadLines(file).Select(line => (file, line)))
            .Where(entry => _mark.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}: {entry.line}")
            .ToList();

        Assert.Empty(offenders);
    }
}
