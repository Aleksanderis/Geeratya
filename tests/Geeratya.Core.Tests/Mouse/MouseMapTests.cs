using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;

namespace Geeratya.Core.Tests.Mouse;

public class MouseMapTests
{
    private const string SolutionFile = "Geeratya.slnx";

    [Fact]
    public void GivenMouseTableInDocument_WhenComparingWithMap_ThenRowsMatchExactly()
    {
        var documented = ReadTableRows();
        var coded = DefaultMouseMap.Rows
            .Select(row => $"{row.Target}|{GestureName(row.Gesture)}|{row.Action}|{row.Part}")
            .ToList();

        Assert.Equal(coded.Order(), documented.Order());
    }

    [Fact]
    public void GivenKeymapActions_WhenCheckingMousePaths_ThenEveryActionHasARowOrAnExclusion()
    {
        var keymap = DefaultKeymap.Load();
        var covered = DefaultMouseMap.Rows.Select(row => row.Action).ToHashSet();
        var missing = keymap.Bindings
            .Select(binding => binding.Action)
            .Distinct()
            .Where(action => !ActionCatalog.IsModalOnly(action))
            .Where(action => !covered.Contains(action) && !DefaultMouseMap.Exclusions.ContainsKey(action))
            .ToList();

        Assert.True(missing.Count == 0, $"No mouse path or exclusion for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void GivenMouseRows_WhenCheckingActions_ThenEveryActionIsInTheCatalog()
    {
        foreach (var row in DefaultMouseMap.Rows)
        {
            Assert.NotNull(ActionCatalog.FindTitle(row.Action));
        }

        foreach (var action in DefaultMouseMap.Exclusions.Keys)
        {
            Assert.NotNull(ActionCatalog.FindTitle(action));
        }
    }

    [Fact]
    public void GivenMouseMap_WhenCheckingDuplicates_ThenEachTargetAndGestureAppearsOnceExceptMenuRows()
    {
        var duplicates = DefaultMouseMap.Rows
            .Where(row => row.Target != MouseTarget.ContextMenu)
            .GroupBy(row => (row.Target, row.Gesture))
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void GivenExclusions_WhenReadingTheDocument_ThenEveryExcludedActionIsNamed()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "ux-mouse.md"));

        var named = DefaultMouseMap.Exclusions.Keys
            .Where(key => !key.StartsWith("tab.go-to.", StringComparison.Ordinal));

        foreach (var action in named)
        {
            Assert.Contains($"`{action}`", text);
        }

        Assert.Contains("`tab.go-to.1` to `tab.go-to.9`", text);
    }

    [Fact]
    public void GivenWheelRows_WhenReading_ThenTheyMoveThreeRows()
    {
        Assert.Equal(3, DefaultMouseMap.Find(MouseTarget.Lists, MouseGesture.WheelDown)!.Count);
    }

    private static string GestureName(MouseGesture gesture)
    {
        return gesture switch
        {
            MouseGesture.WheelUp => "Wheel up",
            MouseGesture.WheelDown => "Wheel down",
            MouseGesture.DoubleClick => "Double-click",
            MouseGesture.RightClick => "Right-click",
            MouseGesture.CtrlClick => "Ctrl-click",
            _ => gesture.ToString(),
        };
    }

    private static List<string> ReadTableRows()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs", "ux-mouse.md");
        var rows = new List<string>();
        var inTable = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("| Target | Gesture", StringComparison.Ordinal))
            {
                inTable = true;

                continue;
            }

            if (!inTable)
            {
                continue;
            }

            if (!line.StartsWith('|'))
            {
                break;
            }

            var cells = line.Trim('|').Split('|').Select(cell => cell.Trim().Trim('`')).ToArray();

            if (cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add($"{cells[0]}|{cells[1]}|{cells[2]}|{cells[3]}");
        }

        return rows;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
