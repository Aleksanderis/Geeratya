using System.Diagnostics;
using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;
using Xunit.Abstractions;

namespace Geeratya.Tui.Tests.Performance;

/// <summary>
/// Opt-in timing of the in-process hot paths of docs/performance.md. Skipped unless GEERATYA_BENCH=1, so
/// <c>dotnet test</c> stays fast and deterministic. Run: <c>GEERATYA_BENCH=1 dotnet test -c Release
/// tests/Geeratya.Tui.Tests --filter HotPathMeasurements --logger "console;verbosity=detailed"</c>. JIT numbers,
/// not Native AOT; with-parent rows sit in the same column as their parent. They exclude the terminal draw and tmux.
/// </summary>
[Trait("Category", "Measurement")]
public sealed class HotPathMeasurements(ITestOutputHelper output)
{
    private const int Rows = 10_000;
    private const int Width = 120;
    private const int Height = 36;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [
            new BoardColumn("To Do", ["1"]),
            new BoardColumn("Doing", ["2"]),
            new BoardColumn("Empty", ["3"]),
            new BoardColumn("Done", ["4"]),
        ],
        "10001",
        null,
        null,
        null);

    private readonly AppReducer _reducer = new(_keymap);

    [MeasurementFact]
    public void ListOf10kRows_NavKeyThroughReducerAndFrame()
    {
        var state = Active(Tab(ViewLayout.List, ViewSourceKind.Jql, withParents: false));

        Report("list 10k rows, nav key + frame", TimeKeys(state, ["Ctrl-d", "j"]));
    }

    [MeasurementTheory]
    [InlineData(false, "board 10k cards, nav key + frame")]
    [InlineData(true, "board 10k cards with sub-tasks under parents, nav key + frame")]
    public void BoardOf10kCards_NavKeyThroughReducerAndFrame(bool withParents, string label)
    {
        var state = Active(Tab(ViewLayout.Columns, ViewSourceKind.Board, withParents));

        Report(label, TimeKeys(state, ["j", "l", "Ctrl-d", "h", "Ctrl-u"]));
    }

    [MeasurementTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoardOf10kCards_RegroupingAloneCostsPerKey(bool withParents)
    {
        var rows = Issues(withParents);
        var samples = new List<double>();

        BoardColumns.Visible(_configuration, rows);

        for (var run = 0; run < 100; run++)
        {
            var start = Stopwatch.GetTimestamp();

            BoardColumns.Visible(_configuration, rows);

            samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        Report($"BoardColumns.Visible alone, 10k cards{(withParents ? " with parents" : string.Empty)}", samples);
    }

    internal static bool Enabled => Environment.GetEnvironmentVariable("GEERATYA_BENCH") == "1";

    private static List<IssueSummary> Issues(bool withParents)
    {
        var rows = new List<IssueSummary>(Rows);

        for (var number = 1; number <= Rows; number++)
        {
            var parent = withParents && number % 5 == 0 && number > 4 ? new IssueParent($"A-{number - 4}", "Parent") : null;

            rows.Add(new IssueSummary(
                number.ToString(CultureInfo.InvariantCulture),
                $"A-{number}",
                $"Summary of issue {number}",
                new IssueStatus((number % 4 + 1).ToString(CultureInfo.InvariantCulture), "Status", "new"),
                new IssueTypeSummary("10", "Task", 0, false),
                null,
                number % 3 == 0 ? new IssueAssignee("acc", "Some Person") : null,
                [],
                parent,
                null,
                "A"));
        }

        return rows;
    }

    private static TabState Tab(ViewLayout layout, ViewSourceKind kind, bool withParents)
    {
        var definition = new ViewDefinition(kind, kind == ViewSourceKind.Board ? "4" : "project = A", layout, "View");
        var rows = Issues(withParents);

        return TabState.Opened(new TabId(1), definition, 1, 1, Height - 4) with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = layout == ViewLayout.Columns ? _configuration : null,
            SelectedKey = rows[0].Key,
            KeyWidth = 7,
        };
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private List<double> TimeKeys(AppState start, string[] keys)
    {
        var state = start;
        var samples = new List<double>();

        for (var index = 0; index < 500 + 2500; index++)
        {
            var begin = Stopwatch.GetTimestamp();

            state = _reducer.Reduce(state, new KeyPressed(keys[index % keys.Length])).State;
            FrameBuilder.BuildStyled(state, Width, Height, _keymap);

            if (index >= 500)
            {
                samples.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
            }
        }

        return samples;
    }

    private void Report(string label, List<double> samples)
    {
        samples.Sort();

        var median = samples[samples.Count / 2];
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{label}: median {median:F2} ms, p95 {p95:F2} ms ({samples.Count} samples)"));
    }
}
