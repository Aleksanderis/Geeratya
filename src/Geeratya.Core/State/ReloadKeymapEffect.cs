namespace Geeratya.Core.State;

/// <summary>Read the user's keymap file again; the host answers with <see cref="KeymapReloaded"/>.</summary>
public sealed record ReloadKeymapEffect : Effect;
