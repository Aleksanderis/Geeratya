using Geeratya.Core.Profiles;
using Geeratya.Core.Text;

namespace Geeratya.Core.State;

public static partial class ProfileReducer
{
    private static string List(ProfileCatalog catalog)
    {
        if (catalog.Profiles.Count == 0)
        {
            return "No profiles yet. Add one with :profile add";
        }

        var names = catalog.Profiles.Select(profile => profile.Name == catalog.ActiveName ? $"{profile.Name} *" : profile.Name);

        return $"Profiles: {string.Join(", ", names)} (* active)";
    }

    private static string? Validate(ProfileForm form, ProfileCatalog catalog)
    {
        return Validate(form, form.Field, catalog);
    }

    private static string? Validate(ProfileForm form, ProfileFormField field, ProfileCatalog catalog)
    {
        return field switch
        {
            ProfileFormField.Name => ValidateName(form.Name.Trim(), form.EditingName, catalog),
            ProfileFormField.SiteUrl => ValidateSite(form.SiteUrl),
            ProfileFormField.Email => ValidateEmail(form.Email.Trim()),
            _ => form.Token.IsEmpty && !form.IsEdit ? TokenMissing : null,
        };
    }

    private static FieldProblem? FirstProblem(ProfileForm form, ProfileCatalog catalog)
    {
        foreach (var field in Enum.GetValues<ProfileFormField>())
        {
            if (Validate(form, field, catalog) is { } text)
            {
                return new FieldProblem(field, text);
            }
        }

        return null;
    }

    /// <summary>Validates as the user types: a field that is still empty shows nothing yet.</summary>
    private static AppState Typed(AppState state, ProfileForm form)
    {
        var empty = form.Field switch
        {
            ProfileFormField.Name => form.Name.Length == 0,
            ProfileFormField.SiteUrl => form.SiteUrl.Length == 0,
            ProfileFormField.Email => form.Email.Length == 0,
            _ => form.Token.IsEmpty,
        };

        var error = empty ? null : Validate(form, state.Profiles);

        var edited = form with { Error = error, Testing = false, TestResult = null, TestFailed = false };

        return state with { Form = edited };
    }

    private static string? ValidateName(string name, string? editing, ProfileCatalog catalog)
    {
        if (name.Length == 0)
        {
            return "The name cannot be empty.";
        }

        return catalog.Find(name) is null || name == editing ? null : $"A profile named {name} already exists.";
    }

    private static string? ValidateSite(string text)
    {
        return TryNormalizeSite(text) is null ? "Enter an https site address such as https://example.atlassian.net." : null;
    }

    private static string? ValidateEmail(string email)
    {
        var at = email.IndexOf('@');

        return at > 0 && at < email.Length - 1 && !email.Contains(' ') ? null : "Enter a valid email address.";
    }

    private static string? TryNormalizeSite(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        var withScheme = trimmed.Contains("://") ? trimmed : $"https://{trimmed}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.UserInfo.Length == 0 ? uri.GetLeftPart(UriPartial.Authority) : null;
    }

    private static ProfileForm Normalize(ProfileForm form)
    {
        return form.Field switch
        {
            ProfileFormField.Name => form with { Name = form.Name.Trim() },
            ProfileFormField.SiteUrl => form with { SiteUrl = TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl },
            ProfileFormField.Email => form with { Email = form.Email.Trim() },
            _ => form,
        };
    }

    private static (ProfileForm Form, bool Changed) EditField(ProfileForm form, Func<PromptText, PromptText> change)
    {
        if (form.Field == ProfileFormField.Token)
        {
            var (secret, back) = form.Token.Edit(form.Back, change);
            var tokenChanged = !ReferenceEquals(secret, form.Token);

            return (form with { Token = secret, Back = back, Error = tokenChanged ? null : form.Error }, tokenChanged);
        }

        var current = form.Field switch
        {
            ProfileFormField.Name => form.Name,
            ProfileFormField.SiteUrl => form.SiteUrl,
            _ => form.Email,
        };
        var prompt = change(new PromptText(current, form.Back));
        var textChanged = prompt.Text != current;
        var moved = form with { Back = prompt.Back, Error = textChanged ? null : form.Error };

        return form.Field switch
        {
            ProfileFormField.Name => (moved with { Name = prompt.Text }, textChanged),
            ProfileFormField.SiteUrl => (moved with { SiteUrl = prompt.Text }, textChanged),
            _ => (moved with { Email = prompt.Text }, textChanged),
        };
    }

    private static AppState Say(AppState state, string message)
    {
        return state with { Message = message };
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }

    private static bool Matches(ProfileForm form, Profile tested)
    {
        var site = TryNormalizeSite(form.SiteUrl) ?? form.SiteUrl;

        return site == tested.SiteUrl && form.Email.Trim() == tested.Email;
    }

    private sealed record FieldProblem(ProfileFormField Field, string Text);
}
