namespace Geeratya.Core.Cache;

/// <summary>The JSON shape of a cached board configuration (see <see cref="CachedViewFile"/>).</summary>
public sealed record CachedBoard(
    long BoardId,
    string Name,
    string Type,
    List<CachedColumn> Columns,
    string FilterId,
    string? SubQuery,
    string? RankFieldId,
    string? ConstraintType,
    string? EstimationFieldName = null);

/// <summary>The JSON shape of a board column.</summary>
public sealed record CachedColumn(string Name, List<string> StatusIds);
