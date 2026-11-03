using Geeratya.Jira.Fields;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>What belongs to one connection and outlives a single request: its transport and the per-connection answers.</summary>
/// <param name="Transport">The transport; null while not connected.</param>
/// <param name="Kinds">Scrum or Kanban per board, seeded from the profile's stored answers.</param>
/// <param name="Fields">The Sprint and estimate field ids of the site, read once.</param>
internal sealed record ConnectionParts(JiraTransport? Transport, BoardKindResolver? Kinds, SiteFields? Fields);
