namespace Geeratya.Jira.RemoteLinks.Models;

/// <summary>A link from an issue to a page outside Jira (Confluence, a web page, another tool).</summary>
/// <param name="Id">The remote link id.</param>
/// <param name="Url">The target URL.</param>
/// <param name="Title">The link title; the URL when Jira sent none.</param>
/// <param name="ApplicationName">The linked application (for example Confluence); null for a plain web link.</param>
public sealed record RemoteLink(string Id, string Url, string Title, string? ApplicationName);
