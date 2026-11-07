using System.Globalization;
using System.Text.Json;

namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// Reads a theme file (<c>docs/internal/ux.md</c>, "Themes"): pure text in, theme or reasons out. A missing colour, a malformed
/// colour, an unknown variant or version or a bad <c>ansi</c> entry refuses the whole file, one reason per problem;
/// unknown keys are ignored. Contrast is not checked here: only the built-in themes are held to it, by tests.
/// </summary>
public static class ThemeFileParser
{
    /// <summary>The only <c>version</c> there is.</summary>
    public const int CurrentVersion = 1;

    private const int HexLength = 7;
    private const char HexPrefix = '#';
    private const string OverlayBackgroundKey = "overlayBackground";
    private const string DarkName = "dark";
    private const string LightName = "light";

    private static readonly ThemeColor[] _ansiColors =
    [
        ThemeColor.Accent,
        ThemeColor.Good,
        ThemeColor.Bad,
        ThemeColor.Remote,
        ThemeColor.Warn,
        ThemeColor.Info,
        ThemeColor.Muted,
        ThemeColor.Border,
    ];

    public static ThemeLoadResult Parse(string name, string text)
    {
        ThemeFileModel? model;

        try
        {
            model = JsonSerializer.Deserialize(text, ThemeJsonContext.Default.ThemeFileModel);
        }
        catch (JsonException exception)
        {
            return Refuse($"not valid JSON ({exception.Message})");
        }

        if (model is null)
        {
            return Refuse("the file is empty");
        }

        var errors = new List<string>();

        if (model.Version is { } version && version != CurrentVersion)
        {
            errors.Add($"version must be {CurrentVersion}");
        }

        var variant = ReadVariant(model.Variant, errors);
        var colors = ReadColors(model.Colors, errors);
        var ansi = ReadAnsi(model.Ansi, errors);
        var overlay = ReadOverlayBackground(model.Colors, errors);

        if (errors.Count > 0 || variant is null)
        {
            return new ThemeLoadResult(null, errors);
        }

        return new ThemeLoadResult(new Theme(name, variant.Value, colors, ansi, overlay), []);
    }

    private static ThemeLoadResult Refuse(string error)
    {
        return new ThemeLoadResult(null, [error]);
    }

    private static ThemeVariant? ReadVariant(string? text, List<string> errors)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case DarkName:
                return ThemeVariant.Dark;
            case LightName:
                return ThemeVariant.Light;
            default:
                errors.Add($"variant must be \"{DarkName}\" or \"{LightName}\"");

                return null;
        }
    }

    private static Dictionary<ThemeColor, Rgb> ReadColors(Dictionary<string, string>? values, List<string> errors)
    {
        var colors = new Dictionary<ThemeColor, Rgb>();

        foreach (var color in Enum.GetValues<ThemeColor>())
        {
            var text = Find(values, color.ToString());

            if (text is null)
            {
                errors.Add($"colors.{ToKey(color)} is missing");
            }
            else if (TryParseHex(text, out var rgb))
            {
                colors[color] = rgb;
            }
            else
            {
                errors.Add($"colors.{ToKey(color)}: '{text}' is not a colour like #RRGGBB");
            }
        }

        return colors;
    }

    private static Rgb? ReadOverlayBackground(Dictionary<string, string>? values, List<string> errors)
    {
        var text = Find(values, OverlayBackgroundKey);

        if (text is null)
        {
            return null;
        }

        if (TryParseHex(text, out var rgb))
        {
            return rgb;
        }

        errors.Add($"colors.{OverlayBackgroundKey}: '{text}' is not a colour like #RRGGBB");

        return null;
    }

    private static Dictionary<ThemeColor, Ansi16Color> ReadAnsi(Dictionary<string, string>? values, List<string> errors)
    {
        var ansi = new Dictionary<ThemeColor, Ansi16Color>();

        foreach (var color in _ansiColors)
        {
            var text = Find(values, color.ToString());

            if (text is null)
            {
                continue;
            }

            if (TryParseName(text.Trim(), out Ansi16Color named))
            {
                ansi[color] = named;
            }
            else
            {
                errors.Add($"ansi.{ToKey(color)}: '{text}' is not one of the 16 colours");
            }
        }

        return ansi;
    }

    private static string? Find(Dictionary<string, string>? values, string name)
    {
        if (values is null)
        {
            return null;
        }

        foreach (var (key, value) in values)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string ToKey(ThemeColor color)
    {
        var name = color.ToString();

        return $"{char.ToLowerInvariant(name[0])}{name[1..]}";
    }

    private static bool TryParseHex(string text, out Rgb rgb)
    {
        rgb = default;

        if (text.Length != HexLength || text[0] != HexPrefix)
        {
            return false;
        }

        if (!int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        rgb = Rgb.FromHex(value);

        return true;
    }

    // Matches one enum name only: no numbers and no comma lists, which Enum.TryParse would accept.
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
}
