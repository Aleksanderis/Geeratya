namespace Geeratya.Core.Jump;

/// <summary>The result of reading jump text.</summary>
/// <param name="Kind">What the text is.</param>
/// <param name="Key">The upper-case issue key when <paramref name="Kind"/> is <see cref="JumpParseKind.Key"/>.</param>
public sealed record JumpParse(JumpParseKind Kind, string Key);
