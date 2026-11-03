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
    private const int ButtonGap = 3;
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

        lines.Add(OverlayBox.Wall(NoteLine(form), contentWidth));
        lines.Add(OverlayBox.Wall(ButtonLine(), contentWidth));
        lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(keymap), TextRole.Muted), contentWidth));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var top = (height - lines.Count) / 2;
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], lines[index], left, width);
        }

        MarkRegions(regions, top, left, boxWidth);

        return result;
    }

    /// <summary>The short status-line text while the box is drawn: the focused field.</summary>
    public static string StatusText(ProfileForm form)
    {
        return Label(form.Field);
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

    /// <summary>What the typed value of the focused field is shown as: the text, or one <c>*</c> per token letter.</summary>
    public static string Shown(ProfileForm form)
    {
        return ShownOf(form, form.Field);
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
        var value = ShownOf(form, field);
        var spans = new List<StyledSpan>
        {
            new(focused ? "> " : "  ", TextRole.Key),
            new(TextWidth.Pad(LabelOf(field), LabelWidth), TextRole.Muted),
        };

        if (field == ProfileFormField.Token && value.Length == 0 && form.IsEdit)
        {
            spans.Add(new StyledSpan(focused ? $"{KeptText}_" : KeptText, TextRole.Muted));

            return StyledLines.Fit(new StyledLine(spans), width);
        }

        var room = Math.Max(0, width - 2 - LabelWidth - 1);

        spans.Add(new StyledSpan(StatusBar.Tail(value, room), focused ? TextRole.Strong : TextRole.Body));

        if (focused)
        {
            spans.Add(new StyledSpan("_", TextRole.Key));
        }

        return StyledLines.Fit(new StyledLine(spans), width);
    }

    private static StyledLine NoteLine(ProfileForm form)
    {
        if (form.Error is { } error)
        {
            return StyledLine.Of($"! {error}", TextRole.Warning);
        }

        if (form.Testing)
        {
            return StyledLine.Of(TestingText, TextRole.Loading);
        }

        if (form.TestResult is { } result)
        {
            return StyledLine.Of(result, form.TestFailed ? TextRole.Error : TextRole.StatusDone);
        }

        return StyledLine.Empty;
    }

    private static StyledLine ButtonLine()
    {
        var gap = new string(' ', ButtonGap);

        return new StyledLine(
        [
            new StyledSpan(TestLabel, TextRole.Key),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(SaveLabel, TextRole.Key),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(CancelLabel, TextRole.Key),
        ]);
    }

    private static void MarkRegions(List<OverlayRegion>? regions, int top, int left, int boxWidth)
    {
        if (regions is null)
        {
            return;
        }

        var fields = Enumerable.Range(0, FieldCount);

        OverlayBox.Mark(regions, MouseTarget.FormField, top, left, boxWidth, 1, fields);

        var buttonRow = top + 1 + FieldCount + 1;
        var column = left + 2;

        foreach (var (target, label) in Buttons())
        {
            var width = TextWidth.Of(label);

            regions.Add(new OverlayRegion(target, buttonRow, column, width, 1, 0));
            column += width + ButtonGap;
        }
    }

    private static IEnumerable<(MouseTarget Target, string Label)> Buttons()
    {
        yield return (MouseTarget.ProfileTestButton, TestLabel);
        yield return (MouseTarget.ProfileSaveButton, SaveLabel);
        yield return (MouseTarget.ProfileCancelButton, CancelLabel);
    }

    private static string FooterText(KeymapTable keymap)
    {
        var field = keymap.FindKeys(InputMode.Insert, ActionIds.FormFieldNext);
        var save = keymap.FindKeys(InputMode.Insert, ActionIds.FormSubmit);
        var test = keymap.FindKeys(InputMode.Insert, ActionIds.ProfileTest);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        return $"{field} field  {save} save  {test} test  {cancel} cancel";
    }
}
