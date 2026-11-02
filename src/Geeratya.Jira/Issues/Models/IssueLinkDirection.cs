namespace Geeratya.Jira.Issues.Models;

/// <summary>Which side of the link type the opened issue is on.</summary>
public enum IssueLinkDirection
{
    /// <summary>The other issue is the inward one: this issue reads the type's inward text ("is blocked by").</summary>
    Inward,

    /// <summary>The other issue is the outward one: this issue reads the type's outward text ("blocks").</summary>
    Outward,
}
