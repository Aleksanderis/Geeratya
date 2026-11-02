namespace Geeratya.Jira.Adf;

/// <summary>A formatting mark on a text node.</summary>
public abstract record AdfMark;

public sealed record AdfStrongMark : AdfMark;

public sealed record AdfEmphasisMark : AdfMark;

public sealed record AdfStrikeMark : AdfMark;

public sealed record AdfCodeMark : AdfMark;

public sealed record AdfUnderlineMark : AdfMark;

public sealed record AdfLinkMark(string? Href) : AdfMark;

public sealed record AdfTextColorMark(string? Color) : AdfMark;

/// <summary>Subscript or superscript; <see cref="Kind" /> is <c>sub</c> or <c>sup</c> as Jira sends it.</summary>
public sealed record AdfSubSupMark(string? Kind) : AdfMark;

/// <summary>A mark this model does not know; kept by name so nothing is lost.</summary>
public sealed record AdfUnknownMark(string TypeName) : AdfMark;
