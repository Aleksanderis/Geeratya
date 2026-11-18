using Geeratya.Core.Editing;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The add and edit profile form (<c>:profile add</c>, <c>Ctrl-a</c> and <c>Ctrl-e</c> in the profile list) as a
/// centred box: one labelled row per field with the focused one marked, the token masked (or "kept" when editing and
/// nothing is typed), a line for the validation note or the connection test result, clickable buttons and the keys.
/// Pure. When it does not fit the caller keeps the whole prompt in the status line.
/// </summary>
public static class ProfileFormOverlay
{
    /// <summary>How many fields the form has (name, site, account, token).</summary>
    public const int FieldCount = 4;

    private const string AddTitle = "Add profile";
    private static readonly ChipButton[] _buttons =
    [
        new(TestLabel, MouseTarget.ProfileTestButton),
        new(SaveLabel, MouseTarget.ProfileSaveButton),
        new(CancelLabel, MouseTarget.ProfileCancelButton),
    ];

    private const string EditTitle = "Edit profile";
    private const string TestLabel = "Test connection";
    private const string SaveLabel = "Save";
    private const string CancelLabel = "Cancel";
    private const string KeptText = "kept (type to replace)";
    private const string TestingText = "Testing the connection...";
    private const int MaxBoxWidth = 72;
    private const int MinBoxWidth = 34;
    private const int Margin = 4;
    private const int LabelWidth = 13;
    private const int BoxRows = 9;
    private const char MaskChar = '*';

    /// <summary>
    /// Draws the form over <paramref name="body"/> (exactly <paramref name="height"/> lines), or returns null when it
    /// does not fit.
    /// </summary>
    public static IReadOnlyList<StyledLine>? Compose(
        IReadOnlyList<StyledLine> body,
        ProfileForm form,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var boxWidth = Math.Min(width - Margin, MaxBoxWidth);

        if (boxWidth < MinBoxWidth || height < BoxRows)
        {
            return null;
        }

        var contentWidth = boxWidth - 2;
        var title = form.IsEdit ? $"{EditTitle} · {form.EditingName}" : AddTitle;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, title) };

        foreach (var field in Enum.GetValues<ProfileFormField>())
        {
            lines.Add(OverlayBox.Wall(FieldLine(form, field, contentWidth - 2), contentWidth));
        }


        // The error shows as every form shows it (FormErrors: wrapped, "! " first, error role); the box grows by the
        // rows it needs while the terminal has them.
        var notes = NoteLines(form, contentWidth, height - BoxRows + 1);

        foreach (var note in notes)
        {
            lines.Add(OverlayBox.Wall(note, contentWidth));
        }

        lines.Add(OverlayBox.Wall(ChipButtons.Line(_buttons), contentWidth));
        lines.Add(OverlayBox.Wall(Footer(keymap), contentWidth));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var top = (height - lines.Count) / 2;
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], lines[index], left, width);
        }

        MarkRegions(regions, top, left, boxWidth, notes.Count);

        return result;
    }

    /// <summary>
    /// The short status-line text while the box is drawn: the focused field, or its error as every form says it
    /// (<c>! Field: message</c>, as <see cref="FormOverlay.StatusText"/>).
    /// </summary>
    public static string StatusText(ProfileForm form)
    {
        return form.Error is { } error ? $"! {Label(form.Field)}: {error}" : Label(form.Field);
    }

    /// <summary>The name of a field as the form and the status line show it.</summary>
    public static string Label(ProfileFormField field)
    {
        return field switch
        {
            ProfileFormField.Name => "Profile name",
            ProfileFormField.SiteUrl => "Site URL",
            ProfileFormField.Email => "Account email",
            _ => "API token (hidden)",
        };
    }

    private static string ShownOf(ProfileForm form, ProfileFormField field)
    {
        return field switch
        {
            ProfileFormField.Name => form.Name,
            ProfileFormField.SiteUrl => form.SiteUrl,
            ProfileFormField.Email => form.Email,
            _ => new string(MaskChar, form.Token.Length),
        };
    }

    /// <summary>What Enter does on <paramref name="field"/>; the status hint uses it.</summary>
    internal static string AcceptVerb(ProfileFormField field) => field == ProfileFormField.Token ? "save" : "next";

    private static string LabelOf(ProfileFormField field)
    {
        return field switch
        {
            ProfileFormField.Name => "Name",
            ProfileFormField.SiteUrl => "Site URL",
            ProfileFormField.Email => "Account",
            _ => "Token",
        };
    }

    private static StyledLine FieldLine(ProfileForm form, ProfileFormField field, int width)
    {
        var focused = field == form.Field;
        var spans = new List<StyledSpan>
        {
            new("  ", TextRole.Body),
            new(TextWidth.Pad(LabelOf(field), LabelWidth), TextRole.Muted),
        };
        var room = Math.Max(0, width - 2 - LabelWidth - 1);

        if (field == ProfileFormField.Token && form.Token.IsEmpty && form.IsEdit)
        {
            spans.Add(new StyledSpan(KeptText, TextRole.Muted));

            if (focused)
            {
                spans.Add(new StyledSpan(" ", TextRole.EditCaret));
            }

            return StyledLines.Fit(new StyledLine(spans), width);
        }

        var prompt = PromptOf(form, field);

        if (!focused)
        {
            spans.Add(new StyledSpan(StatusBar.Tail(prompt.Text, room), TextRole.Body));

            return StyledLines.Fit(new StyledLine(spans), width);
        }

        spans.AddRange(CaretText.Spans(prompt, room));

        return StyledLines.Fit(new StyledLine(spans), width);
    }

    // The text of a field with its caret; the token is one mask character per letter with the caret in the same place.
    internal static TextBuffer PromptOf(ProfileForm form, ProfileFormField field)
    {
        if (field == ProfileFormField.Token)
        {
            var masked = new string(MaskChar, form.Token.Length);

            return TextBuffer.FromBack(masked, form.Token.ElementsAfter(form.Back));
        }

        var text = ShownOf(form, field);

        return field == form.Field ? TextBuffer.FromBack(text, form.Back) : TextBuffer.Of(text);
    }

    private static List<StyledLine> NoteLines(ProfileForm form, int contentWidth, int room)
    {
        if (form.Error is { } error)
        {
            return [.. FormErrors.Lines(error, contentWidth - 1).Take(Math.Max(1, room))];
        }

        if (form.Testing)
        {
            return [StyledLine.Of(TestingText, TextRole.Loading)];
        }

        if (form.TestResult is { } result)
        {
            return [StyledLine.Of(result, form.TestFailed ? TextRole.Error : TextRole.StatusDone)];
        }

        return [StyledLine.Empty];
    }

    private static void MarkRegions(List<OverlayRegion>? regions, int top, int left, int boxWidth, int noteRows)
    {
        if (regions is null)
        {
            return;
        }

        var fields = Enumerable.Range(0, FieldCount);

        OverlayBox.Mark(regions, MouseTarget.FormField, top, left, boxWidth, 1, fields);

        var buttonRow = top + 1 + FieldCount + noteRows;
        ChipButtons.Mark(regions, buttonRow, left + 2, _buttons);
    }

    private static StyledLine Footer(KeymapTable keymap)
    {
        return DialogFooter.Line(
        [
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormFieldNext, HintLabels.Field),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, HintLabels.Save),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.ProfileTest, HintLabels.Test),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Close),
        ]);
    }
}
