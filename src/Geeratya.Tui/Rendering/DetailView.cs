using Geeratya.Core.Detail;

namespace Geeratya.Tui.Rendering;

/// <summary>The window of a laid-out issue document that the detail overlay draws.</summary>
/// <param name="Document">The document, laid out for the drawn width.</param>
/// <param name="First">The first document line shown.</param>
/// <param name="Rows">The document rows the screen has (the zoom strip excluded).</param>
/// <param name="StripRows">The rows the zoom strip takes above the document (0 when not zoomed).</param>
/// <param name="Editing">The pane edited in place, null when none.</param>
internal sealed record DetailView(DetailDocument Document, int First, int Rows, int StripRows, string? Editing);
