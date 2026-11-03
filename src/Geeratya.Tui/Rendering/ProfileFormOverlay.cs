using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The add-profile prompt (<c>:profile add</c>) as a centred box: title with the step, what the answer is for, the
/// field (<c>&gt; text_</c>, the token masked) and the answering keys. Pure. When it does not fit the caller keeps
/// the whole prompt in the status line.
/// </summary>
public static class ProfileFormOverlay
{
    /// <summary>How many steps the form asks (name, URL, email, token).</summary>
    public const int FieldCount = 4;

    private const string Title = "Add profile";
    private const int MaxBoxWidth = 60;
    private const int MinBoxWidth = 24;
    private const int Margin = 4;
    private const int FrameRows = 3;
    private const int ContentRows = 3;
    private const char MaskChar = '*';

    /// <summary>
    /// Draws the prompt of <paramref name="form"/> over <paramref name="body"/> (exactly <paramref name="height"/>
    /// lines), or returns null when it does not fit.
    /// </summary>
    public static IReadOnlyList<StyledLine>? Compose(
        IReadOnlyList<StyledLine> body,
        ProfileForm form,
        KeymapTable keymap,
        int width,
        int height)
    {
        var boxWidth = Math.Min(width - Margin, MaxBoxWidth);
        var spare = height - FrameRows - ContentRows - (form.Error is null ? 0 : 1);

        if (boxWidth < MinBoxWidth || spare < 0)
        {
            return null;
        }

        var contentWidth = boxWidth - 2;
        var lines = new List<StyledLine>
        {
            OverlayBox.Top(boxWidth, $"{Title}  {StepText(form)}"),
            OverlayBox.Wall(StyledLine.Of(Purpose(form.Field), TextRole.Muted), contentWidth),
        };

        if (spare >= 2)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        lines.Add(OverlayBox.Wall(Prompt(form, contentWidth - 2), contentWidth));

        if (form.Error is { } error)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Of($"! {error}", TextRole.Warning), contentWidth));
        }

        if (spare >= 1)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(form, keymap), TextRole.Muted), contentWidth));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var top = (height - lines.Count) / 2;
        var left = (width - boxWidth) / 2;
        var result = body.ToList();

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Place(lines[index], left, width);
        }

        return result;
    }

    /// <summary>The short status-line text while the box is drawn: the step and its name.</summary>
    public static string StatusText(ProfileForm form)
    {
        return $"{StepText(form)} {Label(form.Field)}";
    }

    /// <summary>The step as <c>[2/4]</c>.</summary>
    public static string StepText(ProfileForm form)
    {
        return $"[{(int)form.Field + 1}/{FieldCount}]";
    }

    /// <summary>The name of a step as the status line shows it.</summary>
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

    /// <summary>What the typed value of a step is shown as: the text, or one <c>*</c> per character of the token.</summary>
    public static string Shown(ProfileForm form)
    {
        return form.Field switch
        {
            ProfileFormField.Name => form.Name,
            ProfileFormField.SiteUrl => form.SiteUrl,
            ProfileFormField.Email => form.Email,
            _ => new string(MaskChar, form.Token.Length),
        };
    }

    private static string Purpose(ProfileFormField field)
    {
        return field switch
        {
            ProfileFormField.Name => "A short name for this site.",
            ProfileFormField.SiteUrl => "Your Jira Cloud address.",
            ProfileFormField.Email => "The email of your account.",
            _ => "Your Jira API token (hidden).",
        };
    }

    private static StyledLine Prompt(ProfileForm form, int width)
    {
        var value = StatusBar.Tail(Shown(form), Math.Max(0, width - 3));

        return new StyledLine([new StyledSpan("> ", TextRole.Key), new StyledSpan($"{value}_", TextRole.Body)]);
    }

    private static string FooterText(ProfileForm form, KeymapTable keymap)
    {
        var accept = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);
        var verb = form.Field == ProfileFormField.Token ? "save" : "next";

        return $"{accept} {verb}   {cancel} cancel";
    }
}
