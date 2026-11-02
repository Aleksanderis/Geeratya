using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

/// <summary>
/// What a block needs from its parent: the columns it may use, the role plain text takes and the nesting depth.
/// </summary>
internal readonly record struct RenderContext(int Width, TextRole BaseRole, int Depth)
{
    /// <summary>The context with <paramref name="columns" /> fewer columns (never below 1), one level deeper.</summary>
    public RenderContext Narrow(int columns)
    {
        return this with { Width = Math.Max(1, Width - columns), Depth = Depth + 1 };
    }

    public RenderContext Deeper()
    {
        return this with { Depth = Depth + 1 };
    }

    public RenderContext WithBase(TextRole role)
    {
        return this with { BaseRole = role };
    }

    public RenderContext WithWidth(int width)
    {
        return this with { Width = Math.Max(1, width) };
    }
}
