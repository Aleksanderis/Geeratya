using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Links;

/// <summary>A link type with the side the acting issue is on, as chosen in the link form.</summary>
/// <param name="TypeId">The link type id.</param>
/// <param name="Direction">Which wording the acting issue reads.</param>
/// <param name="Text">That wording, such as "is blocked by".</param>
public sealed record LinkChoice(string TypeId, IssueLinkDirection Direction, string Text);
