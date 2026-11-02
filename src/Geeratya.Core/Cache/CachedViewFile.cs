namespace Geeratya.Core.Cache;

/// <summary>
/// The JSON shape of one cached view file. Cache files have their own DTOs, separate from the Jira models, so a
/// model change breaks <see cref="CacheMapper"/> at compile time instead of silently changing files on disk.
/// </summary>
/// <param name="Schema">The file format version; any other value than <see cref="CacheMapper.Schema"/> is ignored.</param>
/// <param name="Source">The view source kind, spelled as the enum member.</param>
/// <param name="SourceValue">The JQL text, filter id or board id.</param>
/// <param name="SavedAt">When the rows came from the network.</param>
/// <param name="Rows">The cached rows.</param>
/// <param name="Board">The board configuration, if the source is a board.</param>
public sealed record CachedViewFile(
    int Schema,
    string Source,
    string SourceValue,
    DateTimeOffset SavedAt,
    List<CachedIssueRow> Rows,
    CachedBoard? Board);
