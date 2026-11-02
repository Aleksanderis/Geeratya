namespace Geeratya.Core.Keymap;

/// <summary>
/// One way to continue a half-typed binding: the next <paramref name="Key"/> and either the <paramref name="Action"/>
/// it runs or, when more keys follow, the <paramref name="Count"/> of actions below it.
/// </summary>
public sealed record NextKey(string Key, string? Action, int Count);
