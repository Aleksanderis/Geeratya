namespace Geeratya.Core.Jump;

/// <summary>One line of the jump prompt.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="Title">The issue title when known, otherwise empty.</param>
/// <param name="Origin">Where the row comes from.</param>
public sealed record JumpRow(string Key, string Title, JumpOrigin Origin);
