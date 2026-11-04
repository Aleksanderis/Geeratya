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

    /// <summary>A comment: Enter does nothing; the comment keys (edit, delete) act on it.</summary>
    Comment,

    /// <summary>
    /// A field of the issue (<see cref="DetailFieldIds"/>): the cursor rests on it as a whole, however many lines its
    /// value wraps to, and Enter opens it for edit through the existing write paths.
    /// </summary>
    Field,
}

/// <summary>An openable document row.</summary>
/// <param name="Kind">What the row opens.</param>
/// <param name="Value">
/// The issue key (issue and attachment rows), the URL (remote link rows), the comment id (comment rows) or the
/// field id (field rows).
/// </param>
/// <param name="LinkId">The id of the issue link the row shows; null for any other row (#190 removes it).</param>
/// <param name="Expands">The row is a "N more lines" line: Enter shows the rest of the description or comment.</param>
/// <param name="Collapses">The row is a "Show less" line: Enter cuts the description or comment short again.</param>
public sealed record DetailTarget(
    DetailTargetKind Kind,
    string Value,
    string? LinkId = null,
    bool Expands = false,
    bool Collapses = false)
{
    /// <summary>Whether Enter on the row expands or collapses long text instead of its usual action.</summary>
    public bool Toggles => Expands || Collapses;

    /// <summary>The key in <see cref="DetailState.Expanded"/> of the text this row belongs to.</summary>
    public string ExpandKey => Kind == DetailTargetKind.Comment ? $"comment:{Value}" : Value;
}
