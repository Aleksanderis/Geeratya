using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>The lines of one issue detail laid out for one width; the screen shows a window of them.</summary>
/// <param name="Width">The columns the lines were laid out for.</param>
/// <param name="Lines">The styled lines, each at most <paramref name="Width"/> columns wide.</param>
public sealed record DetailDocument(int Width, IReadOnlyList<StyledLine> Lines);
