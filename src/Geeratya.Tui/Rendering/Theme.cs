namespace Geeratya.Tui.Rendering;

/// <summary>A named set of colours: one <see cref="PaletteEntry"/> for every <see cref="PaletteRole"/>.</summary>
public sealed record Theme(string Name, IReadOnlyDictionary<PaletteRole, PaletteEntry> Entries)
{
    public PaletteEntry Get(PaletteRole role)
    {
        return Entries[role];
    }
}
