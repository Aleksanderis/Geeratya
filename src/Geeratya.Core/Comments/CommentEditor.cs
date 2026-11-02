using Geeratya.Core.Editing;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;

namespace Geeratya.Core.Comments;

/// <summary>
/// The multi-line comment editor (Insert mode): the text being written for a new comment or the new text of an
/// existing one. It lives in <c>AppState.Editor</c> until the write ended or the text was discarded; a failed write
/// keeps it open with the text and the reason.
/// </summary>
/// <param name="Tab">The tab the editor was opened from; the write is started for it.</param>
/// <param name="Key">The issue the comment belongs to.</param>
/// <param name="CommentId">The id of the comment being edited, null for a new comment.</param>
/// <param name="Buffer">The text and the cursor.</param>
/// <param name="Original">The text the editor opened with (empty for a new comment); the editor is changed when it differs.</param>
/// <param name="Warning">What the user should know before saving (formatting lost, someone else's comment), or null.</param>
/// <param name="Error">Why the last save did not happen, or null.</param>
/// <param name="OperationId">The id of the write in flight, 0 while nothing is being saved.</param>
/// <param name="Suggest">The open suggestion popup of the trigger before the cursor (@ or #), or null.</param>
/// <param name="Dismissed">The text index of a trigger whose popup Esc closed (not reopened for it), or -1.</param>
public sealed record CommentEditor(
    TabId Tab,
    string Key,
    string? CommentId,
    TextBuffer Buffer,
    string Original,
    string? Warning = null,
    string? Error = null,
    long OperationId = 0,
    SuggestionState? Suggest = null,
    int Dismissed = -1)
{
    private const int DefaultColumns = 80;
    private const int NarrowColumns = 24;
    private const int Margin = 4;
    private const int MaxBoxWidth = 100;
    private const int WallsAndPadding = 4;

    public bool IsEdit => CommentId is not null;

    /// <summary>Whether the text differs from the one the editor opened with.</summary>
    public bool IsChanged => !string.Equals(Buffer.Text, Original, StringComparison.Ordinal);

    public bool IsSaving => OperationId != 0;

    /// <summary>The width of the box for a terminal of <paramref name="columns"/> columns (0 when not known yet).</summary>
    public static int BoxWidth(int columns)
    {
        var width = columns > 0 ? columns : DefaultColumns;

        return width >= NarrowColumns ? Math.Min(width - Margin, MaxBoxWidth) : width;
    }

    /// <summary>The columns of text inside the box (between the walls and their padding).</summary>
    public static int InnerWidth(int columns)
    {
        return Math.Max(1, BoxWidth(columns) - WallsAndPadding);
    }
}
