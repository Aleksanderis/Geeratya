namespace Geeratya.Core.Text;

/// <summary>
/// What a filled field block shows: a label above a value on a tinted block (<c>docs/ux-screens.md</c>, "Details
/// pane"; <c>docs/ux-look.md</c>, section 6).
/// </summary>
/// <param name="Label">The field name, drawn in the label role.</param>
/// <param name="Items">
/// The value as items that wrap as a whole (a label chip, a person, a word); a single value is one item. An item wider
/// than the block is cut.
/// </param>
/// <param name="Editable">The field opens a popup: the block ends in the dropdown mark.</param>
/// <param name="Note">A muted word after the label (<c>moves via transition</c>), null for none.</param>
public sealed record FieldBlockSpec(
    string Label,
    IReadOnlyList<IReadOnlyList<StyledSpan>> Items,
    bool Editable,
    string? Note = null)
{
    /// <summary>A block whose value is one item.</summary>
    public static FieldBlockSpec OfValue(string label, IReadOnlyList<StyledSpan> value, bool editable, string? note = null)
    {
        return new FieldBlockSpec(label, [value], editable, note);
    }
}

/// <summary>
/// The filled field block as a reusable widget: the label row, then the value rows on a tint one step off the page,
/// each exactly the block width, the first cell of a value row left free for the focus bar. The Details pane uses it
/// and the create and edit forms reuse it (#708). Pure: the same spec and width give the same rows.
/// </summary>
public static class FieldBlock
{
    /// <summary>The mark of an editable block (ASCII <c>v</c> through the glyph pass).</summary>
    public const string Dropdown = "▾";

    /// <summary>The focus bar (ASCII <c>&gt;</c>).</summary>
    public const string Bar = "▌";

    private const string Lead = " ";
    private const int DropdownCells = 2;
    private const int MinDropdownWidth = 5;
    private const string LabelGap = "  ";

    /// <summary>
    /// The rows of the block: the label row (label and note, cut to <paramref name="width"/>) and one or more value
    /// rows. Value rows are padded with the tint to exactly <paramref name="width"/> cells.
    /// </summary>
    public static IReadOnlyList<StyledLine> Rows(FieldBlockSpec spec, int width)
    {
        var columns = Math.Max(1, width);
        var rows = new List<StyledLine> { LabelRow(spec, columns) };
        var dropdown = spec.Editable && columns >= MinDropdownWidth;
        var room = columns - Lead.Length - (dropdown ? DropdownCells : 0);
        var current = new List<StyledSpan>();
        var used = 0;

        foreach (var item in room < 1 ? [] : spec.Items)
        {
            var fitted = Cut(item, room);
            var size = Width(fitted);

            if (current.Count > 0 && used + 1 + size > room)
            {
                rows.Add(ValueRow(current, columns, false));
                current = [];
                used = 0;
            }

            if (current.Count > 0)
            {
                current.Add(new StyledSpan(" ", TextRole.ValueBlock));
                used++;
            }

            current.AddRange(fitted);
            used += size;
        }

        rows.Add(ValueRow(current, columns, dropdown));

        return rows;
    }

    /// <summary>
    /// A value row of a focused block: the lead cell becomes the accent bar and the tint becomes the selection, so
    /// focus is not colour alone. Border spans and chips keep their roles. Rows without a lead cell (the label row)
    /// are returned as they are.
    /// </summary>
    public static StyledLine Focus(StyledLine row, bool ascii)
    {
        var spans = new List<StyledSpan>(row.Spans.Count);
        var barred = false;

        foreach (var span in row.Spans)
        {
            if (!barred && span is { Role: TextRole.ValueBlock, Text: Lead })
            {
                spans.Add(new StyledSpan(ascii ? ">" : Bar, TextRole.SelectionBar));
                barred = true;
            }
            else
            {
                spans.Add(span.Role == TextRole.ValueBlock ? span with { Role = TextRole.Selected } : span);
            }
        }

        return barred ? new StyledLine(spans) : row;
    }

    private static StyledLine LabelRow(FieldBlockSpec spec, int columns)
    {
        var label = DisplayText.Cut(spec.Label, columns);
        var spans = new List<StyledSpan> { new(label, TextRole.FieldLabel) };
        var room = columns - DisplayWidth.Of(label) - LabelGap.Length;

        if (spec.Note is { Length: > 0 } note && room >= 1)
        {
            spans.Add(new StyledSpan($"{LabelGap}{DisplayText.Cut(note, room)}", TextRole.Muted));
        }

        return new StyledLine(spans);
    }

    private static StyledLine ValueRow(List<StyledSpan> items, int columns, bool dropdown)
    {
        var spans = new List<StyledSpan>(items.Count + 3) { new(Lead, TextRole.ValueBlock) };

        spans.AddRange(items);

        var used = Lead.Length + Width(items);

        if (dropdown)
        {
            spans.Add(new StyledSpan($" {Dropdown}", TextRole.ValueBlock));
            used += DropdownCells;
        }

        spans.Add(new StyledSpan(new string(' ', Math.Max(0, columns - used)), TextRole.ValueBlock));

        return new StyledLine(spans);
    }

    /// <summary>The spans cut to <paramref name="room"/> cells: the span that overflows ends in an ellipsis.</summary>
    private static List<StyledSpan> Cut(IReadOnlyList<StyledSpan> item, int room)
    {
        var fitted = new List<StyledSpan>(item.Count);
        var used = 0;

        foreach (var span in item)
        {
            var left = room - used;

            if (left <= 0)
            {
                break;
            }

            var text = DisplayText.Cut(span.Text, left);

            fitted.Add(span with { Text = text });
            used += DisplayWidth.Of(text);

            if (text.Length < span.Text.Length)
            {
                break;
            }
        }

        return fitted;
    }

    private static int Width(IEnumerable<StyledSpan> spans)
    {
        return spans.Sum(static span => DisplayWidth.Of(span.Text));
    }
}
