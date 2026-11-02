namespace Geeratya.Core.Sources;

/// <summary>One palette command: its name, usage line and a one-line description.</summary>
public sealed record PaletteCommand(string Name, string Usage, string Description);
