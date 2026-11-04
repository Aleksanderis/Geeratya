namespace Geeratya.Core.Detail;

/// <summary>How much of a long text shows: its line count and the width the notes under it wrap to.</summary>
/// <param name="Lines">
/// Lines shown before the rest is cut; with <paramref name="NoteRow"/> the note counts as one.
/// </param>
/// <param name="Width">The columns inside the panel.</param>
/// <param name="NoteRow">
/// The "more lines" note takes one of <paramref name="Lines"/> rows (a pane of fixed height).
/// </param>
/// <param name="ZoomHint">The "more lines" note also names <c>&lt;space&gt;z zoom</c>.</param>
internal sealed record LongTextLimit(int Lines, int Width, bool NoteRow = false, bool ZoomHint = false);
