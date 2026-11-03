using System.Globalization;
using Geeratya.Core.Suggestions;
using Geeratya.Jira.Suggestions.Models;
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

    private static readonly char[] _lineBreaks = ['\r', '\n'];

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
            return field with { Entry = Bounded($"{field.Entry}{FirstLine(text)}"), Error = null };
        }

        if (FormFieldKinds.IsPicked(field.Kind))
        {
            return field with { Entry = Bounded($"{field.Entry}{FirstLine(text)}"), Error = null };
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
            return BackspaceChoice(field);
        }

        if (FormFieldKinds.IsPicked(field.Kind))
        {
            return BackspacePicked(field);
        }

        return field with { Value = FormValue.OfText(RemoveLast(field.Value.Text)), Error = null };
    }

    /// <summary>Enter: adds the typed label, or moves on (a choice is taken from the suggestion popup).</summary>
    public static FormEdit Accept(FormField field)
    {
        if (!field.IsEditable)
        {
            return new FormEdit(field, true);
        }

        return field.Kind switch
        {
            FormFieldKind.Paragraph => new FormEdit(Type(field, "\n"), false),
            FormFieldKind.Labels => AcceptLabels(field),
            FormFieldKind.User or FormFieldKind.Parent => new FormEdit(field, field.Entry.Length == 0),
            _ => new FormEdit(field, true),
        };
    }

    /// <summary>The field with a label that is typed and not yet added made part of its value.</summary>
    public static FormField Committed(FormField field)
    {
        return field.Kind == FormFieldKind.Labels ? CommitLabel(field) : field;
    }

    /// <summary>
    /// Takes the suggestion the user accepted: a label is added to the labels, a person or issue becomes the value
    /// (its name is kept as an option so the form can show it). The typing buffer is emptied.
    /// </summary>
    public static FormField Choose(FormField field, Suggestion suggestion)
    {
        if (!field.IsEditable)
        {
            return field;
        }

        if (field.Kind == FormFieldKind.Labels)
        {
            return CommitLabel(field with { Entry = suggestion.Id });
        }

        if (FormFieldKinds.IsChoice(field.Kind))
        {
            return ChooseOption(field, suggestion);
        }

        if (!FormFieldKinds.IsPicked(field.Kind))
        {
            return field;
        }

        if (suggestion.Id.Length == 0)
        {
            return field with { Value = FormValue.Empty, Entry = string.Empty, Error = null };
        }

        var option = new FieldOption(suggestion.Id, SuggestionText.Of(suggestion));

        return field with
        {
            Value = FormValue.OfChosen([suggestion.Id]),
            Options = [.. field.Options.Where(other => other.Id != suggestion.Id), option],
            Entry = string.Empty,
            Error = null,
        };
    }

    // A single choice replaces its value (the "(none)" row clears it); a multiple choice adds a chip.
    private static FormField ChooseOption(FormField field, Suggestion suggestion)
    {
        // A value found by a search may not be among the allowed values: keep its label so the form can show it.
        if (suggestion.Id.Length > 0 && field.Options.All(option => option.Id != suggestion.Id))
        {
            var option = new FieldOption(suggestion.Id, SuggestionText.Of(suggestion));

            field = field with { Options = [.. field.Options, option] };
        }

        if (field.Kind == FormFieldKind.Select)
        {
            var value = suggestion.Id.Length == 0 ? FormValue.Empty : FormValue.OfChosen([suggestion.Id]);

            return field with { Value = value, Entry = string.Empty, Error = null };
        }

        if (suggestion.Id.Length == 0 || field.Value.Chosen.Contains(suggestion.Id))
        {
            return field with { Entry = string.Empty };
        }

        var chosen = FormValue.OfChosen([.. field.Value.Chosen, suggestion.Id]);

        return field with { Value = chosen, Entry = string.Empty, Error = null };
    }

    // Backspace edits the typed filter; on an empty filter it clears a single choice or drops the last chip.
    private static FormField BackspaceChoice(FormField field)
    {
        if (field.Entry.Length > 0)
        {
            return field with { Entry = RemoveLast(field.Entry), Error = null };
        }

        var keep = field.Kind == FormFieldKind.Select ? 0 : Math.Max(0, field.Value.Chosen.Count - 1);

        return field with { Value = FormValue.OfChosen(field.Value.Chosen.Take(keep)), Error = null };
    }

    private static FormField BackspacePicked(FormField field)
    {
        if (field.Entry.Length > 0)
        {
            return field with { Entry = RemoveLast(field.Entry), Error = null };
        }

        return field with { Value = FormValue.Empty, Error = null };
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(_lineBreaks);

        return end < 0 ? text : text[..end];
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
