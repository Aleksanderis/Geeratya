using Geeratya.Core.Editing;
using Geeratya.Core.Forms;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// A pane of the detail in its edit state (<c>docs/internal/ux-screens.md</c>, "Editing in place"): the field the open form
/// edits in place, drawn with its caret instead of the pane's normal content. Pure.
/// </summary>
/// <param name="Pane">The pane (<see cref="PaneIds.Title"/> or <see cref="PaneIds.Description"/>).</param>
/// <param name="Form">The form that holds the text, its errors and its save state.</param>
public sealed record PaneEdit(string Pane, FormState Form)
{
    /// <summary>Body rows of the Description editor when the pane is not zoomed.</summary>
    public const int EditorRows = 8;

    private const string SaveKey = "Ctrl-s";
    private const string SaveText = " save";
    private const string CancelKey = "Esc";
    private const string CancelText = " cancel";
    private const string Gap = "   ";
    private const string EditingText = "editing";
    private const string SavingText = "Saving…";

    private FormField Field => Form.Fields[Math.Clamp(Form.Focus, 0, Form.Fields.Count - 1)];

    /// <summary>The lines of the text being edited.</summary>
    public int TextRows => Field.Value.Text.Count(static character => character == '\n') + 1;

    /// <summary>
    /// The edit of the open form when it edits a pane of the issue <paramref name="key"/> in place, else null.
    /// </summary>
    public static PaneEdit? Of(FormState? form, string key)
    {
        if (form is not { Pane: { } pane, IsLoading: false, Fields.Count: > 0 }
            || !string.Equals(form.Target.IssueKey, key, StringComparison.Ordinal))
        {
            return null;
        }

        return new PaneEdit(pane, form);
    }

    /// <summary>The pane the open form edits in place on the issue <paramref name="key"/>, else null.</summary>
    public static string? PaneOf(FormState? form, string key)
    {
        return Of(form, key)?.Pane;
    }

    /// <summary>The dim chip at the right of the top edge: the pane is being edited.</summary>
    public static IReadOnlyList<StyledSpan> Chip()
    {
        return [new StyledSpan(EditingText, TextRole.Muted)];
    }

    /// <summary>The summary as one row with the caret, then the notices.</summary>
    public List<StyledLine> TitleRows(int inner)
    {
        var field = Field;
        var rows = FieldRows.Value(field, Look(inner));

        rows.AddRange(Footer(inner));

        return rows;
    }

    /// <summary>
    /// The description as the rows of its editor: a window of at most <paramref name="maxRows"/> text rows with
    /// the caret row in the middle of it (at the ends of the text it sits nearer the edge), then the notices.
    /// <paramref name="scroll"/> is set when the text has more rows.
    /// </summary>
    public List<StyledLine> DescriptionRows(int inner, int maxRows, out PaneScroll? scroll)
    {
        var field = Field;
        var prompt = TextBuffer.FromBack(field.Value.Text, field.Back);
        var lines = field.Value.Text.Split('\n');
        var caretRow = prompt.Line;
        var notices = Footer(inner);
        var room = Math.Max(1, maxRows - notices.Count);
        var shown = Math.Min(room, lines.Length);
        var first = Math.Clamp(caretRow - (shown / 2), 0, lines.Length - shown);
        var rows = FieldRows.Paragraph(field, Look(inner), first, shown);

        rows.AddRange(notices);
        scroll = lines.Length > shown ? new PaneScroll(first, lines.Length, shown) : null;

        return rows;
    }

    private static FieldLook Look(int inner)
    {
        return new FieldLook(Math.Max(1, inner)) { Editing = true, Boxed = true };
    }

    // The rows under the input: the key hint, then the notices.
    private List<StyledLine> Footer(int inner)
    {
        var rows = new List<StyledLine> { Hint(inner) };

        rows.AddRange(Notices(inner));

        return rows;
    }

    private static StyledLine Hint(int inner)
    {
        if (DisplayWidth.Of(SaveKey + SaveText + Gap + CancelKey + CancelText) > inner)
        {
            return StyledLine.Of(DisplayText.Cut(SaveKey + SaveText, inner), TextRole.Muted);
        }

        return new StyledLine(
        [
            new StyledSpan(SaveKey, TextRole.Key),
            new StyledSpan(SaveText + Gap, TextRole.Muted),
            new StyledSpan(CancelKey, TextRole.Key),
            new StyledSpan(CancelText, TextRole.Muted),
        ]);
    }

    private List<StyledLine> Notices(int inner)
    {
        var notices = new List<StyledLine>();

        if (Form.IsSaving)
        {
            notices.Add(StyledLine.Of(SavingText, TextRole.Loading));
        }

        if (Field.Error is { } error)
        {
            notices.AddRange(FieldRows.Error(error, inner));
        }

        if (Form.TopError is { } top)
        {
            notices.AddRange(FieldRows.Error(top, inner));
        }

        return notices;
    }
}
