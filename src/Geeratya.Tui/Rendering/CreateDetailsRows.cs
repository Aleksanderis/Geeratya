using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Suggestions;
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
    private const int PopupRows = 5;
    private const int PopupMargin = 2;

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

        if (focused && !_form.Navigating)
        {
            AddPopup(rows, index, inner);
        }

        if (_form.Fields[index].Error is { } error)
        {
            rows.Add(new CreatePaneRow(StyledLine.Of($"! {error}", TextRole.Error), MouseTarget.FormField, index));
        }
    }

    // The suggestion popup of the focused field: ranked rows, the cursor row marked, a status row.
    private void AddPopup(List<CreatePaneRow> rows, int index, int inner)
    {
        if (_form.Suggest is not { } popup || popup.FieldIndex != index)
        {
            return;
        }

        var width = Math.Max(1, inner - 2 - PopupMargin);
        var margin = new string(' ', PopupMargin);
        var cursor = Math.Clamp(popup.Cursor, -1, Math.Max(0, popup.Items.Count - 1));
        var first = FormOverlay.PopupFirst(popup);
        var shown = Math.Min(PopupRows, Math.Max(0, popup.Items.Count - first));

        foreach (var item in Enumerable.Range(first, shown))
        {
            var text = TextWidth.Fit(SuggestionText.Row(popup.Items[item]), width);
            var highlighted = item == cursor;
            var pointer = " ";
            var role = highlighted ? TextRole.Selected : TextRole.Body;
            var line = StyledLine.Of($"{margin}{pointer} {text}", role);

            rows.Add(new CreatePaneRow(line, MouseTarget.FormChoice, item));
        }

        if (FormOverlay.PopupStatus(popup) is { } status)
        {
            rows.Add(new CreatePaneRow(StyledLine.Of($"{margin}  {TextWidth.Fit(status.Text, width)}", status.Role)));
        }
    }

    private StyledLine? FoldHint(int inner)
    {
        if (!CreatePanes.CanFold(_form))
        {
            return null;
        }

        var key = _keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormMoreFields) ?? "Ctrl-f";
        var action = $"{key} {(_form.ShowAllFields ? FewerText : MoreText)}";
        var names = _form.ShowAllFields
            ? string.Empty
            : string.Join(Separator, CreatePanes.Folded(_form).Select(index => _form.Fields[index].Label));
        var room = inner - DisplayWidth.Of(action) - 3;
        var spans = new List<StyledSpan>();

        if (names.Length > 0 && room >= 4)
        {
            spans.Add(new StyledSpan($"{DisplayText.Cut(names, room)}   ", TextRole.Muted));
        }

        spans.Add(new StyledSpan(key, TextRole.Key));
        spans.Add(new StyledSpan(action[key.Length..], TextRole.Muted));

        return new StyledLine(spans);
    }
}
