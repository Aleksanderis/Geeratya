namespace Geeratya.Tui.Rendering;

/// <summary>
/// The hint chips of one surface, left to right. When the status line is too narrow the chips before the last
/// <paramref name="Kept"/> ones drop from the end, the kept ones (the way out, the write) go last.
/// </summary>
public sealed record SurfaceChips(IReadOnlyList<HintChip> Chips, int Kept)
{
    /// <summary>A surface that keeps its last chip (the way out) on a narrow line; chips without a key are left out.</summary>
    public static SurfaceChips KeepLast(IEnumerable<HintChip?> chips)
    {
        var list = chips.OfType<HintChip>().ToList();

        return new SurfaceChips(list, Math.Min(1, list.Count));
    }
}
