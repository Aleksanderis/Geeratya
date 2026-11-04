using Geeratya.Jira.Fields;
using Geeratya.Jira.Gateway;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>What belongs to one connection and outlives a request: its transport and per-connection answers.</summary>
/// <param name="Transport">The transport; null while not connected.</param>
/// <param name="Kinds">Scrum or Kanban per board, seeded from the profile's stored answers.</param>
/// <param name="Fields">The Sprint and estimate field ids of the site, read once.</param>
/// <param name="Cutoffs">Jira's Done cutoff per board, read through the gateway and kept for the connection.</param>
internal sealed record ConnectionParts(
    JiraTransport? Transport,
    BoardKindResolver? Kinds,
    SiteFields? Fields,
    DoneCutoffReader? Cutoffs);
