namespace Geeratya.Jira.Adf;

/// <summary>Where a token sits in the editor text: from <paramref name="Start"/> up to, not including, <paramref name="End"/>.</summary>
public sealed record AdfTokenSpan(int Start, int End);
