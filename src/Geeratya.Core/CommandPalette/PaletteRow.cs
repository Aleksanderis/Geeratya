namespace Geeratya.Core.CommandPalette;

/// <summary>One line of the palette list.</summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Id">The action id (Action), the command line to run (Command) or empty.</param>
/// <param name="Title">The title, the group name (Header) or the usage text (Usage).</param>
/// <param name="Keys">The bindings in Normal mode, joined with a comma; empty when unbound.</param>
/// <param name="Enabled">False for an action bound ahead of its feature (drawn greyed out, refuses to run).</param>
public sealed record PaletteRow(PaletteRowKind Kind, string Id, string Title, string Keys, bool Enabled = true)
{
    public bool IsSelectable => Kind is PaletteRowKind.Action or PaletteRowKind.Command;
}
