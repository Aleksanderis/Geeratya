namespace Geeratya.FakeJira;

/// <summary>The recorded files of one Scrum board of the dev site (board 5 team-managed, 6 company-managed).</summary>
public sealed record FakeScrumBoard(
    int Id,
    string BoardFixture,
    string ConfigurationFixture,
    string IssuesFixture,
    string EpicsFixture,
    string SprintsFixture);
