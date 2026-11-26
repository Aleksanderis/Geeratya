using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The rows of the Details pane of the create form: one filled block per shown field (<see cref="CreateFieldBlocks"/>),
/// a blank row between blocks, the error of a field and the open suggestion popup under its block, and the pinned fold
/// hint at the bottom (<c>Due date · Components   Ctrl-f show more fields</c>). Pure.
/// </summary>
internal sealed class CreateDetailsRows
{
    private const string Title = "Details";
    private const string MoreText = "show more fields";
    private const string FewerText = "fewer fields";
    private const string Separator = " · ";
    private const int PanePadding = 2;

    private readonly FormState _form;
    private readonly KeymapTable _keymap;
    private readonly bool _ascii;

    public CreateDetailsRows(FormState form, KeymapTable keymap, bool ascii)
    {
        _form = form;
        _keymap = keymap;
        _ascii = ascii;
    }

    /// <summary>The pane for a content width of <paramref name="inner"/> columns.</summary>
    public CreatePaneSpec Pane(int inner, bool focused)
    {
        var rows = new List<CreatePaneRow>();
        var focusStart = 0;
        var focusLength = 0;

        foreach (var index in CreatePanes.Shown(_form, PaneIds.Details))
        {
            if (rows.Count > 0)
            {
                rows.Add(new CreatePaneRow(StyledLine.Empty));
            }

            var start = rows.Count;

            AddField(rows, index, inner);

            if (index == _form.Focus)
            {
                focusStart = start;
                focusLength = rows.Count - start;
            }
        }

        return new CreatePaneSpec([new StyledSpan(Title, TextRole.Heading)], null, rows, focused)
        {
            FocusStart = focusStart,
            FocusLength = focusLength,
            Pinned = FoldHint(inner),
        };
    }

    private void AddField(List<CreatePaneRow> rows, int index, int inner)
    {
        var focused = index == _form.Focus && !_form.IsSaving;
        var block = FieldBlock.Rows(CreateFieldBlocks.Spec(_form, index, inner), inner);

        for (var row = 0; row < block.Count; row++)
        {
            var line = focused && row > 0 ? FieldBlock.Focus(block[row], _ascii) : block[row];

            rows.Add(new CreatePaneRow(line, MouseTarget.FormField, index));
        }

        // The picker box runs edge to edge of the pane (the two padding columns included), as in the Details dropdown.
        if (focused && PickerBox.Draw(_form, inner + PanePadding) is { } picker)
        {
            AddPicker(rows, picker);

            return;
        }

        if (_form.Fields[index].Error is { } error)
        {
            var errorRows = FieldRows.Error(error, inner);

            rows.AddRange(errorRows.Select(line => new CreatePaneRow(line, MouseTarget.FormField, index)));
        }
    }

    // The open picker of the focused field: a box under it, the rows of its list clickable.
    private static void AddPicker(List<CreatePaneRow> rows, PickerLines picker)
    {
        for (var line = 0; line < picker.Lines.Count; line++)
        {
            var item = picker.Items[line];

            if (item >= 0)
            {
                rows.Add(new CreatePaneRow(picker.Lines[line], MouseTarget.FormChoice, item) { Bleed = true });
            }
            else
            {
                rows.Add(new CreatePaneRow(picker.Lines[line]) { Bleed = true });
            }
        }
    }

    private string NameOf(int index)
    {
        return FormFieldKinds.Named(_form.Fields[index]);
    }

    private StyledLine? FoldHint(int inner)
    {
        if (!CreatePanes.CanFold(_form))
        {
            return null;
        }

        var key = _keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormMoreFields) ?? "Ctrl-f";
        var action = $"{key} {(_form.ShowAllFields ? FewerText : MoreText)}";
        var room = inner - DisplayWidth.Of(action) - 3;
        var spans = new List<StyledSpan>();
        var names = _form.ShowAllFields ? string.Empty : WholeNames(room);

        if (names.Length > 0)
        {
            spans.Add(new StyledSpan($"{names}   ", TextRole.Muted));
        }

        spans.Add(new StyledSpan(key, TextRole.Key));
        spans.Add(new StyledSpan(action[key.Length..], TextRole.Muted));

        return new StyledLine(spans);
    }

    // The folded names that fit in full, then an ellipsis when more follow; a name is never cut in the middle.
    private string WholeNames(int room)
    {
        var folded = CreatePanes.Folded(_form).Select(NameOf).ToList();
        var text = string.Empty;

        for (var count = 0; count < folded.Count; count++)
        {
            var candidate = text.Length == 0 ? folded[count] : $"{text}{Separator}{folded[count]}";
            var suffix = count < folded.Count - 1 ? DisplayText.Ellipsis : string.Empty;

            if (DisplayWidth.Of(candidate) + DisplayWidth.Of(suffix) > room)
            {
                return text.Length > 0 && DisplayWidth.Of(text) + DisplayWidth.Of(DisplayText.Ellipsis) <= room
                    ? $"{text}{DisplayText.Ellipsis}"
                    : string.Empty;
            }

            text = candidate;
        }

        return text;
    }
}
