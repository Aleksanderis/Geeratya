namespace Geeratya.Jira.Adf;

/// <summary>
/// A parsed ADF document. <see cref="IsTruncated" /> is true when nesting beyond the parser's depth limit
/// (or JSON too deep or malformed to read) was dropped.
/// </summary>
public sealed record AdfDocument(int Version, IReadOnlyList<AdfNode> Content, bool IsTruncated)
{
    /// <summary>The document of an empty or <c>null</c> description.</summary>
    public static AdfDocument Empty { get; } = new(1, [], false);
}
