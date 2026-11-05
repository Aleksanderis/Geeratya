using Geeratya.Core.Forms;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// A pane of the detail in its edit state (<c>docs/ux-screens.md</c>, "Editing in place"): the field the open form
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
    private const string DiscardText = "Discard the changes? (y/n)";

    private FormField Field => Form.Fields[Math.Clamp(Form.Focus, 0, Form.Fields.Count - 1)];

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
        var rows = new List<StyledLine> { CaretRows.Field(new PromptText(field.Value.Text, field.Back), Math.Max(1, inner)) };

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
        var prompt = new PromptText(field.Value.Text, field.Back);
        var lines = field.Value.Text.Split('\n');
        var caretRow = prompt.Text[..prompt.Caret].Count(static character => character == '\n');
        var notices = Footer(inner);
        var room = Math.Max(1, maxRows - notices.Count);
        var shown = Math.Min(room, lines.Length);
        var first = Math.Clamp(caretRow - (shown / 2), 0, lines.Length - shown);
        var rows = new List<StyledLine>(shown + notices.Count);

        for (var index = first; index < first + shown; index++)
        {
            rows.Add(CaretRows.FieldRow(prompt, lines[index], index, caretRow, Math.Max(1, inner)));
        }

        rows.AddRange(notices);
        scroll = lines.Length > shown ? new PaneScroll(first, lines.Length, shown) : null;

        return rows;
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
            notices.Add(StyledLine.Of(DisplayText.Cut($"! {error}", inner), TextRole.Error));
        }

        if (Form.TopError is { } top)
        {
            notices.Add(StyledLine.Of(DisplayText.Cut($"! {top}", inner), TextRole.Error));
        }

        if (Form.DiscardPending)
        {
            notices.Add(StyledLine.Of(DiscardText, TextRole.Warning));
        }

        return notices;
    }
}
