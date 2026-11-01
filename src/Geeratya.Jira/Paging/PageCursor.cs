using System.Globalization;

namespace Geeratya.Jira.Paging;

/// <summary>
/// An opaque token that continues a paged read. For token-paged search it is Jira's <c>nextPageToken</c> and only valid
/// with the query that produced it; for offset-paged endpoints (agile API) it carries the next <c>startAt</c>.
/// </summary>
public sealed record PageCursor(string Token)
{
    /// <summary>A cursor for an offset-paged endpoint.</summary>
    public static PageCursor Offset(int startAt) => new(startAt.ToString(CultureInfo.InvariantCulture));

    /// <summary>The offset this cursor carries; false when it is a search token or not a non-negative number.</summary>
    public bool TryGetOffset(out int startAt) =>
        int.TryParse(Token, NumberStyles.None, CultureInfo.InvariantCulture, out startAt);
}
