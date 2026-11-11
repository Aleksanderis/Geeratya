namespace Geeratya.Core.Adf;

/// <summary>The start of an ADF rendering (<see cref="AdfRenderer.RenderHead"/>).</summary>
/// <param name="Rendering">The lines laid out.</param>
/// <param name="Complete">True when nothing was left out, so <paramref name="Rendering"/> is the whole document.</param>
public sealed record AdfHead(AdfRendering Rendering, bool Complete);
