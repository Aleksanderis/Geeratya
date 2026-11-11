using System.Diagnostics;
using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;
using Xunit.Abstractions;

namespace Geeratya.Tui.Tests.Performance;

/// <summary>
/// Opt-in timing of a bulk write on a large tab (#931): 1000 of 10 000 rows moved to a sprint, through the reducer
/// (start, settle) and through the merge of a page load that lands while the write runs. Run like
/// <see cref="HotPathMeasurements"/>, filter <c>BulkWriteMeasurements</c>.
/// </summary>
[Trait("Category", "Measurement")]
public sealed class BulkWriteMeasurements(ITestOutputHelper output)
{
    private const int Rows = 10_000;
    private const int Bulk = 1000;
    private const int Warmup = 5;
    private const int Samples = 30;

    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueSprint _sprint = new(8, "Sprint 8", SprintState.Future);

    private readonly AppReducer _reducer = new(_keymap);

    [MeasurementFact]
    public void ThousandOfTenThousand_StartSettleAndLoadMerge()
    {
        var rows = Issues();
        var keys = rows.Take(Bulk).Select(row => row.Key).ToArray();
        var state = Active(rows);
        var request = new SprintMoveRequest(keys, _sprint, "the backlog");
        var started = _reducer.Reduce(state, new WriteRequested(new TabId(1), request, _now, true)).State;
        var operation = Assert.Single(started.Session.Operations);
        var fresh = rows.Take(Bulk).ToArray();

        var failure = new WriteError(WriteErrorKind.Validation, 400, "no", [], []);

        Report(
            "start: 1000 of 10k rows",
            Time(() => _reducer.Reduce(state, new WriteRequested(new TabId(1), request, _now, true))));
        Report(
            "settle: WriteSucceeded with 1000 rows",
            Time(() => _reducer.Reduce(started, new WriteSucceeded(operation.Id, _now, fresh))));
        Report(
            "fail: rollback of 1000 changes",
            Time(() => _reducer.Reduce(started, new WriteFailed(operation.Id, _now, failure))));
        Report(
            "load merge: 10k incoming rows while the write runs",
            Time(() => WriteMerge.KeepHeldRows(started, started.Session.Tabs[0], rows)));
    }

    private static List<IssueSummary> Issues()
    {
        var rows = new List<IssueSummary>(Rows);

        for (var number = 1; number <= Rows; number++)
        {
            rows.Add(new IssueSummary(
                number.ToString(CultureInfo.InvariantCulture),
                $"A-{number}",
                $"Summary of issue {number}",
                new IssueStatus("1", "To Do", "new"),
                new IssueTypeSummary("10", "Task", 0, false),
                null,
                null,
                [],
                null,
                null,
                "A"));
        }

        return rows;
    }

    private static AppState Active(List<IssueSummary> rows)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "View");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 32) with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Marks = [.. rows.Take(Bulk).Select(row => row.Key)],
            SelectedKey = rows[0].Key,
            KeyWidth = 7,
        };
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static List<double> Time(Func<object> action)
    {
        var samples = new List<double>();

        for (var index = 0; index < Warmup + Samples; index++)
        {
            var begin = Stopwatch.GetTimestamp();

            action();

            if (index >= Warmup)
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
