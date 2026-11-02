using System.Globalization;
using System.Text.Json;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Reads and checks a user theme file. Every problem is collected (unknown role, bad colour, too little contrast); a
/// theme with a problem is refused as a whole.
/// </summary>
public static class ThemeFile
{
    private const int HexLength = 7;
    private const char HexPrefix = '#';

    public static ThemeLoadResult Parse(string name, string json)
    {
        ThemeFileModel? model;

        try
        {
            model = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.ThemeFileModel);
        }
        catch (JsonException exception)
        {
            return Refuse($"not valid JSON ({exception.Message})");
        }

        if (model is null)
        {
            return Refuse("the file is empty");
        }

        var baseTheme = Themes.Find(model.Base ?? Themes.DarkName);

        if (baseTheme is null)
        {
            var known = string.Join(", ", Themes.All.Select(theme => theme.Name));

            return Refuse($"unknown base theme '{model.Base}'; use {known}");
        }

        var errors = new List<string>();
        var entries = new Dictionary<PaletteRole, PaletteEntry>(baseTheme.Entries);

        foreach (var (roleName, role) in model.Roles ?? [])
        {
            if (!TryParseName<PaletteRole>(roleName, out var paletteRole))
            {
                errors.Add($"unknown role '{roleName}'");

                continue;
            }

            if (role is null)
            {
                errors.Add($"role '{roleName}' is empty");

                continue;
            }

            var entry = Merge(roleName, entries[paletteRole], role, errors);

            entries[paletteRole] = entry;
            CheckContrast(paletteRole, roleName, entry, errors);
            CheckSelectionStyle(paletteRole, roleName, entry, errors);
        }

        if (errors.Count > 0)
        {
            return new ThemeLoadResult(null, errors);
        }

        return new ThemeLoadResult(new Theme(name, entries), []);
    }

    private static ThemeLoadResult Refuse(string error)
    {
        return new ThemeLoadResult(null, [error]);
    }

    private static PaletteEntry Merge(string roleName, PaletteEntry baseEntry, ThemeRoleModel role, List<string> errors)
    {
        return new PaletteEntry(
            ReadColour(roleName, "foreground", role.Foreground, baseEntry.TrueForeground, errors),
            ReadColour(roleName, "background", role.Background, baseEntry.TrueBackground, errors),
            ReadBasic(roleName, "foreground16", role.Foreground16, baseEntry.Foreground16, errors),
            ReadBasic(roleName, "background16", role.Background16, baseEntry.Background16, errors),
            ReadStyle(roleName, role.Style, baseEntry.NoColorStyle, errors));
    }

    private static Rgb ReadColour(string roleName, string part, string? text, Rgb fallback, List<string> errors)
    {
        if (text is null)
        {
            return fallback;
        }

        if (text.Length == HexLength
            && text[0] == HexPrefix
            && int.TryParse(
                text.AsSpan(1),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return Rgb.FromHex(value);
        }

        errors.Add($"{roleName}.{part}: '{text}' is not a colour like #RRGGBB");

        return fallback;
    }

    private static Ansi16Color ReadBasic(
        string roleName,
        string part,
        string? text,
        Ansi16Color fallback,
        List<string> errors)
    {
        if (text is null)
        {
            return fallback;
        }

        if (TryParseName<Ansi16Color>(text, out var colour))
        {
            return colour;
        }

        errors.Add($"{roleName}.{part}: '{text}' is not one of the 16 colours ({NamesOf16()})");

        return fallback;
    }

    private static RoleStyle ReadStyle(string roleName, string[]? names, RoleStyle fallback, List<string> errors)
    {
        if (names is null)
        {
            return fallback;
        }

        var style = RoleStyle.None;

        foreach (var name in names)
        {
            if (TryParseName<RoleStyle>(name, out var flag) && flag != RoleStyle.None)
            {
                style |= flag;
            }
            else
            {
                errors.Add($"{roleName}.style: '{name}' is not bold, underline, reverse or italic");
            }
        }

        return style;
    }

    private static void CheckContrast(PaletteRole role, string roleName, PaletteEntry entry, List<string> errors)
    {
        var required = PaletteTable.RequiredContrast(role);
        var trueRatio = Contrast.Ratio(entry.TrueForeground, entry.TrueBackground);

        if (trueRatio < required)
        {
            errors.Add($"{roleName}: contrast {trueRatio:F2} in 24-bit colour is below {required:F1}");
        }

        var basicRatio = Contrast.Ratio(Ansi16.ToRgb(entry.Foreground16), Ansi16.ToRgb(entry.Background16));

        if (basicRatio < required)
        {
            errors.Add($"{roleName}: contrast {basicRatio:F2} in 16 colours is below {required:F1}");
        }
    }

    // Selection and the status bar must stand out without colour (NO_COLOR), so they stay reversed.
    private static void CheckSelectionStyle(PaletteRole role, string roleName, PaletteEntry entry, List<string> errors)
    {
        if (role is PaletteRole.Selected or PaletteRole.Status && !entry.NoColorStyle.HasFlag(RoleStyle.Reverse))
        {
            errors.Add($"{roleName}.style: must include reverse, so it shows without colour");
        }
    }

    // Matches a single enum name only: no numbers and no comma lists, which Enum.TryParse would accept.
    private static bool TryParseName<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(name);

                return true;
            }
        }

        return false;
    }

    private static string NamesOf16()
    {
        return string.Join(", ", Enum.GetNames<Ansi16Color>());
    }
}
