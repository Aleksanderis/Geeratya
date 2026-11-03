using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Backlog;

/// <summary>Rows and sprints for the backlog section tests: an active sprint, two future ones, the backlog.</summary>
internal static class BacklogFixtures
{
    public const long ActiveId = 7;
    public const long FutureId = 8;
    public const long EmptyFutureId = 9;

    public static Sprint Active { get; } = new(
        ActiveId,
        "Sprint 1",
        SprintState.Active,
        "Ship the login",
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero),
        null,
        6);

    public static Sprint Future { get; } = new(FutureId, "Sprint 2", SprintState.Future, null, null, null, null, 6);

    public static Sprint EmptyFuture { get; } =
        new(EmptyFutureId, "Sprint 3", SprintState.Future, null, null, null, null, 6);

    /// <summary>Rows as Jira ranks them: the sprints and the backlog mixed.</summary>
    public static IReadOnlyList<IssueSummary> RankedRows { get; } =
    [
        Issue("A-1", null, 2),
        Issue("A-2", Future, 3),
        Issue("A-3", Active, 5),
        Issue("A-4", Active, null),
        Issue("A-5", null, 1.5),
        Issue("A-6", Future, null),
    ];

    public static TabState Tab(
        IReadOnlyList<IssueSummary> rows,
        IReadOnlyList<Sprint>? sprints = null,
        int viewport = 20)
    {
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board");

        return TabState.Opened(new TabId(1), definition, 1, 1, viewport) with
        {
            Rows = rows,
            Selected = 0,
            SelectedKey = rows.Count > 0 ? rows[0].Key : null,
            KeyWidth = 4,
            Load = LoadState.Idle,
            RequestId = 0,
            Sprints = sprints ?? [],
        };
    }

    public static IssueSummary Issue(string key, Sprint? sprint, double? estimate)
    {
        IssueSprint[] sprints = sprint is null ? [] : [new IssueSprint(sprint.Id, sprint.Name, sprint.State)];

        return new IssueSummary(
            key,
            key,
            $"Issue {key}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A")
        {
            Sprints = sprints,
            Estimate = estimate,
        };
    }
}
