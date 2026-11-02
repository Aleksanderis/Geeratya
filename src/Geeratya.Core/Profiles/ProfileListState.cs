namespace Geeratya.Core.Profiles;

/// <summary>The open profile list (<c>:profile list</c>, Insert mode overlay).</summary>
/// <param name="Selected">The highlighted profile; clamped to the catalog when read.</param>
public sealed record ProfileListState(int Selected);
