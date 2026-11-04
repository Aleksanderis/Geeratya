using Geeratya.Core.Boards;
using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// What the Details pane of the create form shows for a field: the filled block of the detail's Details pane
/// (<see cref="FieldBlockSpec"/>), the label with its required mark, the value as chips or text, the caret while the
/// field is edited. Pure.
/// </summary>
internal static class CreateFieldBlocks
{
    private const string RequiredMark = " *";
    private const string NoneText = "none";
    private const string FromSelection = "from selection";
    private const string NavigateNote = "[navigate]";
    private const string EditingNote = "[editing]";
    private const string UnassignedChip = "[--]";
    private const string UnassignedName = "Unassigned";
    private const int InitialsCells = 2;
    private const int Lead = 1;
    private const int DropdownCells = 2;

    /// <summary>The block of the field at <paramref name="index"/>, <paramref name="width"/> columns wide.</summary>
    public static FieldBlockSpec Spec(FormState form, int index, int width)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var editing = focused && !form.Navigating;
        var room = Math.Max(1, width - Lead - DropdownCells);
        var label = field.Required ? $"{field.Label}{RequiredMark}" : field.Label;

        return new FieldBlockSpec(label, Items(field, editing, room), Dropdown(field), Note(form, field, focused));
    }

    /// <summary>The field opens a popup (its value is chosen, not typed): the block ends in the dropdown.</summary>
    private static bool Dropdown(FormField field)
    {
        return field.Kind is FormFieldKind.Select or FormFieldKind.MultiSelect or FormFieldKind.Labels
            or FormFieldKind.User or FormFieldKind.Parent;
    }

    private static string? Note(FormState form, FormField field, bool focused)
    {
        if (focused && form.Navigable)
        {
            return form.Navigating ? NavigateNote : EditingNote;
        }

        var fromSelection = field.Id == FormFieldIds.Parent && form.Target is CreateFormTarget { ParentKey: { } parent }
            && field.Value.Chosen is [var chosen] && chosen == parent;

        return fromSelection ? FromSelection : null;
    }

    private static List<IReadOnlyList<StyledSpan>> Items(FormField field, bool editing, int room)
    {
        var items = field.Kind switch
        {
            FormFieldKind.Labels => LabelItems(field),
            FormFieldKind.User => UserItems(field),
            FormFieldKind.Select or FormFieldKind.MultiSelect or FormFieldKind.Parent => ChoiceItems(field),
            _ => TextItems(field, editing, room),
        };

        if (editing && field.Kind is FormFieldKind.Labels or FormFieldKind.User or FormFieldKind.Parent
            or FormFieldKind.Select or FormFieldKind.MultiSelect)
        {
            var typed = FormOverlay.Caretted(new PromptText(field.Entry, field.Back), room);

            items.Add([Block(typed)]);
        }

        return items.Count == 0 ? [[new StyledSpan(NoneText, TextRole.Muted)]] : items;
    }

    private static List<IReadOnlyList<StyledSpan>> TextItems(FormField field, bool editing, int room)
    {
        var text = field.Value.Text;

        if (editing)
        {
            var row = field.Kind == FormFieldKind.Paragraph ? Caretted(field, room) : FormOverlay.Caretted(
                new PromptText(text, field.Back),
                room);

            return [[Block(row)]];
        }

        if (text.Length == 0)
        {
            return [];
        }

        var head = text.Split('\n')[0];

        return [[Block(text.Contains('\n') ? $"{head}…" : head)]];
    }

    private static string Caretted(FormField field, int room)
    {
        var prompt = new PromptText(field.Value.Text, field.Back);
        var rows = prompt.Text.Split('\n');
        var caretRow = prompt.Text[..prompt.Caret].Count(character => character == '\n');

        return FormOverlay.ParagraphRow(prompt, rows[caretRow], caretRow, caretRow, room);
    }

    private static List<IReadOnlyList<StyledSpan>> LabelItems(FormField field)
    {
        return [.. field.Value.Chosen.Select(label => (IReadOnlyList<StyledSpan>)
            [new StyledSpan($"[{Clean(FormOverlay.LabelOf(field, label))}]", LabelChips.For(label))])];
    }

    private static List<IReadOnlyList<StyledSpan>> UserItems(FormField field)
    {
        if (field.Value.Chosen.Count == 0)
        {
            return
            [
                [
                    new StyledSpan(UnassignedChip, TextRole.UnassignedChip),
                    new StyledSpan($" {UnassignedName}", TextRole.Muted),
                ],
            ];
        }

        return [.. field.Value.Chosen.Select(id => Person(Clean(FormOverlay.LabelOf(field, id))))];
    }

    private static IReadOnlyList<StyledSpan> Person(string name)
    {
        var chip = $"[{AssigneeInitials.Of(name).PadRight(InitialsCells)}]";

        return [new StyledSpan(chip, TextRole.InitialsChip), new StyledSpan($" {name}", TextRole.ValueBlock)];
    }

    private static List<IReadOnlyList<StyledSpan>> ChoiceItems(FormField field)
    {
        return [.. field.Value.Chosen.Select(id => Choice(field, id))];
    }

    private static IReadOnlyList<StyledSpan> Choice(FormField field, string id)
    {
        var name = Clean(FormOverlay.LabelOf(field, id));

        if (field.Kind == FormFieldKind.Parent)
        {
            return [Block($"[{name}]")];
        }

        if (field.Id != FormFieldIds.Priority)
        {
            return [Block(name)];
        }

        var tier = PriorityTierOf.Name(name);

        return tier switch
        {
            PriorityTier.High => [new StyledSpan("▲", TextRole.Warning), Block($" {name}")],
            PriorityTier.Medium => [new StyledSpan("=", TextRole.ValueBlock), Block($" {name}")],
            PriorityTier.Low => [new StyledSpan("▼", TextRole.Muted), Block($" {name}")],
            _ => [Block(name)],
        };
    }

    private static StyledSpan Block(string text)
    {
        return new StyledSpan(text, TextRole.ValueBlock);
    }

    private static string Clean(string text)
    {
        return string.Concat(text.Where(static character => !char.IsControl(character)));
    }
}
