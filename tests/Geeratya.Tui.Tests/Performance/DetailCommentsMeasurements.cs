using System.Diagnostics;
using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;
using Xunit.Abstractions;

namespace Geeratya.Tui.Tests.Performance;

/// <summary>
/// Opt-in timing of a key press in the issue detail with the most comments Jira lets us load (#929): through the
/// reducer and the frame. Run like <see cref="HotPathMeasurements"/>, filter <c>DetailCommentsMeasurements</c>.
/// </summary>
[Trait("Category", "Measurement")]
public sealed class DetailCommentsMeasurements(ITestOutputHelper output)
{
    private const int CommentCount = 1000;
    private const int Lines = 12;
    private const int Width = 120;
    private const int Height = 36;
    private const int Warmup = 20;
    private const int Samples = 200;

    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(_keymap);

    [MeasurementFact]
    public void ThousandComments_KeyThroughReducerAndFrame()
    {
        var state = InCommentsPane();

        Report("1000 comments, j/k in Comments + frame", TimeKeys(state, ["j", "k"]));
        Report("1000 comments, Ctrl-d/Ctrl-u in Comments + frame", TimeKeys(state, ["Ctrl-d", "Ctrl-u"]));
        Report("1000 comments, Ctrl-j/Ctrl-k between panes + frame", TimeKeys(state, ["Ctrl-k", "Ctrl-j"]));
    }

    [MeasurementFact]
    public void ThousandComments_FirstBuildOfTheRows()
    {
        var detail = DetailState.Opened("A-1", null, 1, 2, 3) with
        {
            Comments = Enumerable.Range(1, CommentCount).Select(Comment).ToArray(),
            LoadedAt = _now,
        };
        var samples = new List<double>();

        for (var index = 0; index < Warmup + 40; index++)
        {
            var begin = Stopwatch.GetTimestamp();

            CommentRows.Build(detail, Width - 4, CommentRows.BodyLines, new DetailRenderMemo());

            if (index >= Warmup)
            {
                samples.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
            }
        }

        Report("1000 comments, first build of the rows (fresh memo)", samples);
    }

    private static IssueComment Comment(int number)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, Lines).Select(line => $"- item {line} of comment {number}"));

        return new IssueComment(
            number.ToString(CultureInfo.InvariantCulture),
            _eve,
            AdfMarkdownParser.Parse(markdown),
            _now.AddHours(-2),
            _now.AddHours(-2));
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            _eve,
            _eve,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);
    }

    private AppState InCommentsPane()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var profiles = ProfileCatalog.Empty.With(profile);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profile) };
        var sized = _reducer.Reduce(start, new ViewportChanged(Height, Width)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = Active(opened);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var listed = Deliver(opened, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now));
        var shown = Deliver(listed, new KeyPressed("Enter"));
        var detail = Active(shown).Detail[0];
        var loaded = Deliver(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now));
        var comments = Enumerable.Range(1, CommentCount).Select(Comment).ToArray();
        var page = new OffsetPage<IssueComment>(comments, 0, CommentCount, CommentCount, true);
        var state = Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now));

        for (var step = 0; step < 20 && PaneOf(state) != PaneIds.Comments; step++)
        {
            state = Deliver(state, new KeyPressed("Ctrl-j"));
        }

        return state;
    }

    private static string PaneOf(AppState state)
    {
        var top = Active(state).Detail[^1];

        return top.Document!.SectionIds[top.Document.SectionAt(top.Cursor)];
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    private List<double> TimeKeys(AppState start, string[] keys)
    {
        var state = start;
        var samples = new List<double>();

        for (var index = 0; index < Warmup + Samples; index++)
        {
            var begin = Stopwatch.GetTimestamp();

            state = _reducer.Reduce(state, new KeyPressed(keys[index % keys.Length])).State;
            FrameBuilder.BuildStyled(state, Width, Height, _keymap);

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
