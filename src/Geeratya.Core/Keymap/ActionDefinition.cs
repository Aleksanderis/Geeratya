namespace Geeratya.Core.Keymap;

/// <summary>
/// An action's id, its user-visible title and its command level; help and the key reference are generated from these.
/// </summary>
public sealed record ActionDefinition(string Id, string Title, CommandLevel Level = CommandLevel.Everyday);
