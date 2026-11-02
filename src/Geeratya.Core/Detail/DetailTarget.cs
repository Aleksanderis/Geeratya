namespace Geeratya.Core.Detail;

/// <summary>What a document row opens.</summary>
public enum DetailTargetKind
{
    /// <summary>A related issue (sub-task, child, linked issue): Enter pushes its detail.</summary>
    Issue,

    /// <summary>A remote link: opens its URL in the browser.</summary>
    RemoteLink,

    /// <summary>An attachment: opens the issue page in the browser; the file is never fetched or drawn.</summary>
    Attachment,
}

/// <summary>An openable document row.</summary>
/// <param name="Kind">What the row opens.</param>
/// <param name="Value">The issue key (issue and attachment rows) or the URL (remote link rows).</param>
/// <param name="LinkId">The id of the issue link the row shows; null for any other row (#190 removes it).</param>
public sealed record DetailTarget(DetailTargetKind Kind, string Value, string? LinkId = null);
