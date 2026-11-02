using System.Globalization;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// Editing one field: pure functions from a field and an input to the changed field, so the whole editor runs without
/// a terminal. Text is bounded to <see cref="MaxTextLength"/> (Jira's own limit). Read-only kinds ignore every input.
/// </summary>
public static class FormEditor
{
    /// <summary>The longest text of a field.</summary>
    public const int MaxTextLength = 32767;

    /// <summary>
    /// The options a choice field shows: those whose label contains the filter, and for a single choice that is not
    /// required a first row that clears it (only while no filter is typed).
    /// </summary>
    public static IReadOnlyList<FieldOption> VisibleOptions(FormField field)
    {
        // The options of a searchable field are the answer of the search for the typed text, so no second filter.
        var matching = field.Searchable ? field.Options : Filtered(field);

        var offersNone = field is { Kind: FormFieldKind.Select, Required: false, Entry.Length: 0 };

        return offersNone ? [new FieldOption(string.Empty, field.NoneLabel), .. matching] : matching;
    }

    /// <summary>Typed or pasted text goes to the value, the filter or the label buffer, as the kind says.</summary>
    public static FormField Type(FormField field, string text)
    {
        if (!field.IsEditable || text.Length == 0)
        {
            return field;
        }

        if (field.Kind == FormFieldKind.Labels)
        {
            return TypeLabels(field, text);
        }

        if (FormFieldKinds.IsChoice(field.Kind))
        {
            return field with { Entry = Bounded($"{field.Entry}{text}"), Cursor = 0 };
        }

        return field with { Value = FormValue.OfText(Bounded($"{field.Value.Text}{text}")), Error = null };
    }

    /// <summary>Deletes the last character; in an empty label buffer it removes the last label.</summary>
    public static FormField Backspace(FormField field)
    {
        if (!field.IsEditable)
        {
            return field;
        }

        if (field.Kind == FormFieldKind.Labels)
        {
            return BackspaceLabels(field);
        }

        if (FormFieldKinds.IsChoice(field.Kind))
        {
            var shortened = field with { Entry = RemoveLast(field.Entry), Cursor = 0 };

            if (shortened is { Searchable: true, Entry.Length: 0 })
            {
                return WithOptions(shortened, shortened.Suggested);
            }

            return shortened;
        }

        return field with { Value = FormValue.OfText(RemoveLast(field.Value.Text)), Error = null };
    }

    /// <summary>Moves the highlighted option of a choice field; stays inside the visible options.</summary>
    public static FormField Move(FormField field, int delta)
    {
        if (!FormFieldKinds.IsChoice(field.Kind))
        {
            return field;
        }

        var last = Math.Max(0, VisibleOptions(field).Count - 1);

        return field with { Cursor = Math.Clamp(field.Cursor + delta, 0, last) };
    }

    /// <summary>Enter: chooses or toggles the highlighted option, adds the typed label, or moves on.</summary>
    public static FormEdit Accept(FormField field)
    {
        if (!field.IsEditable)
        {
            return new FormEdit(field, true);
        }

        return field.Kind switch
        {
            FormFieldKind.Paragraph => new FormEdit(Type(field, "\n"), false),
            FormFieldKind.Select => AcceptSelect(field),
            FormFieldKind.MultiSelect => new FormEdit(Toggle(field), false),
            FormFieldKind.Labels => AcceptLabels(field),
            _ => new FormEdit(field, true),
        };
    }

    /// <summary>
    /// A searchable field with the options of a search reply. The chosen options stay listed, so the value never
    /// loses its label when the reply does not name it.
    /// </summary>
    public static FormField WithOptions(FormField field, IReadOnlyList<FieldOption> options)
    {
        var kept = field.Options
            .Where(option => field.Value.Chosen.Contains(option.Id) && options.All(other => other.Id != option.Id));

        return field with { Options = [.. options, .. kept], Cursor = 0, Searching = false };
    }

    /// <summary>The field with a label that is typed and not yet added made part of its value.</summary>
    public static FormField Committed(FormField field)
    {
        return field.Kind == FormFieldKind.Labels ? CommitLabel(field) : field;
    }

    private static List<FieldOption> Filtered(FormField field)
    {
        var matching = field.Options
            .Where(option => option.Label.Contains(field.Entry, StringComparison.OrdinalIgnoreCase));

        return [.. matching];
    }

    private static List<FieldOption> Retained(FormField field, FieldOption option)
    {
        var rest = field.Suggested.Where(other => other.Id != option.Id);

        return option.Id.Length == 0 ? [.. rest] : [option, .. rest];
    }

    private static FormEdit AcceptSelect(FormField field)
    {
        if (field.Searching)
        {
            return new FormEdit(field, false);
        }

        var visible = VisibleOptions(field);

        if (visible.Count == 0)
        {
            return new FormEdit(field, true);
        }

        var option = visible[Math.Clamp(field.Cursor, 0, visible.Count - 1)];
        var chosen = option.Id.Length == 0 ? FormValue.Empty : FormValue.OfChosen([option.Id]);

        var picked = field with { Value = chosen, Entry = string.Empty, Cursor = 0, Error = null };

        // The search results go away with the typed text; the chosen option stays, as the suggestions around it.
        return new FormEdit(field.Searchable ? picked with { Options = Retained(field, option) } : picked, true);
    }

    private static FormField Toggle(FormField field)
    {
        var visible = VisibleOptions(field);

        if (visible.Count == 0)
        {
            return field;
        }

        var id = visible[Math.Clamp(field.Cursor, 0, visible.Count - 1)].Id;
        var chosen = field.Value.Chosen.Contains(id) ? Without(field.Value.Chosen, id) : [.. field.Value.Chosen, id];

        return field with { Value = FormValue.OfChosen(chosen), Error = null };
    }

    private static List<string> Without(IReadOnlyList<string> chosen, string id)
    {
        return [.. chosen.Where(other => other != id)];
    }

    private static FormEdit AcceptLabels(FormField field)
    {
        return field.Entry.Trim().Length == 0 ? new FormEdit(field, true) : new FormEdit(CommitLabel(field), false);
    }

    private static FormField TypeLabels(FormField field, string text)
    {
        var next = field;

        foreach (var element in Elements(text))
        {
            if (element.Trim().Length == 0)
            {
                next = CommitLabel(next);
            }
            else
            {
                next = next with { Entry = Bounded($"{next.Entry}{element}") };
            }
        }

        return next with { Error = null };
    }

    private static FormField BackspaceLabels(FormField field)
    {
        if (field.Entry.Length > 0)
        {
            return field with { Entry = RemoveLast(field.Entry), Error = null };
        }

        var remaining = field.Value.Chosen.Take(Math.Max(0, field.Value.Chosen.Count - 1));

        return field with { Value = FormValue.OfChosen(remaining), Error = null };
    }

    private static FormField CommitLabel(FormField field)
    {
        var label = field.Entry.Trim();

        if (label.Length == 0)
        {
            return field with { Entry = string.Empty };
        }

        var chosen = field.Value.Chosen.Contains(label) ? field.Value.Chosen : [.. field.Value.Chosen, label];

        return field with { Value = FormValue.OfChosen(chosen), Entry = string.Empty };
    }

    private static List<string> Elements(string text)
    {
        var result = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);

        while (enumerator.MoveNext())
        {
            result.Add(enumerator.GetTextElement());
        }

        return result;
    }

    private static string Bounded(string text)
    {
        if (text.Length <= MaxTextLength)
        {
            return text;
        }

        // Never end on half of a surrogate pair.
        var length = char.IsHighSurrogate(text[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;

        return text[..length];
    }

    private static string RemoveLast(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var starts = StringInfo.ParseCombiningCharacters(text);

        return text[..starts[^1]];
    }
}
