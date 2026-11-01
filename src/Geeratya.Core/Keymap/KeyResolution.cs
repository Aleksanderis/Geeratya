namespace Geeratya.Core.Keymap;

/// <summary>The outcome of feeding one key to the keymap.</summary>
/// <param name="Kind">Whether the key completed a binding, started a longer one or matched nothing.</param>
/// <param name="Action">The action id when <paramref name="Kind"/> is <see cref="KeyResolutionKind.Action"/>.</param>
public sealed record KeyResolution(KeyResolutionKind Kind, string? Action);
