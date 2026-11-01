namespace Geeratya.Core.Keymap;

/// <summary>An action's id and its user-visible title; help and the key reference are generated from these.</summary>
public sealed record ActionDefinition(string Id, string Title);
