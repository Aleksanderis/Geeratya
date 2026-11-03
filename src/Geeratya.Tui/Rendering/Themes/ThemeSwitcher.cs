namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// The choices behind <c>:theme</c> and the start: which theme a saved or typed name gives and what the status line
/// says about it. Pure on top of a <see cref="ThemeCatalog"/>, so the host only draws the answer.
/// </summary>
public sealed class ThemeSwitcher(ThemeCatalog catalog)
{
    private const int MaximumReasons = 3;
    private const char FamilySeparator = '-';

    /// <summary>
    /// The theme for the start: the saved one, or the default dark one with a notice when the name is gone or its
    /// file is invalid (a theme must never stop the start). Theme files that were not offered are reported as well.
    /// </summary>
    public ThemeStart Start(string? savedName, bool? lightBackground = null)
    {
        var notices = new List<string>();

        if (catalog.Problems.Count > 0)
        {
            notices.Add($"Theme files not used: {Reasons(catalog.Problems)}");
        }

        if (string.IsNullOrWhiteSpace(savedName))
        {
            return new ThemeStart(BuiltInThemes.Default, Join(notices));
        }

        var result = catalog.Find(BuiltInThemes.IsAuto(savedName) ? ForBackground(lightBackground) : savedName);

        if (result?.Theme is { } theme)
        {
            return new ThemeStart(theme, Join(notices));
        }

        var reason = result is null ? "not found" : Reasons(result.Errors);

        notices.Add($"Theme {savedName}: {reason}; using {BuiltInThemes.DefaultDarkName}");

        return new ThemeStart(BuiltInThemes.Default, Join(notices));
    }

    /// <summary>
    /// What <c>:theme [name]</c> does. No name: the families on offer. An exact name (or <c>dark</c>, <c>light</c>)
    /// switches. <c>auto</c> switches to the theme for the background (light or dark, null counts as dark) and keeps the
    /// name <c>auto</c> for saving. Anything else lists the names that contain the text, and nothing changes.
    /// </summary>
    public ThemeChoice Choose(string name, string current, bool? lightBackground = null)
    {
        if (name.Length == 0)
        {
            return new ThemeChoice(null, current, List(current));
        }

        if (BuiltInThemes.IsAuto(name))
        {
            return ChooseAuto(lightBackground);
        }

        var result = catalog.Find(name);

        if (result is null)
        {
            return new ThemeChoice(null, name, NotFound(name));
        }

        if (result.Theme is not { } theme)
        {
            return new ThemeChoice(null, name, $"Theme {name} not used: {Reasons(result.Errors)}.");
        }

        return new ThemeChoice(theme, theme.Name, $"Theme {theme.Name}.");
    }

    /// <summary>The built-in theme <c>auto</c> stands for: light for a light background, dark otherwise.</summary>
    public static string ForBackground(bool? lightBackground)
    {
        return lightBackground == true ? BuiltInThemes.DefaultLightName : BuiltInThemes.DefaultDarkName;
    }

    private ThemeChoice ChooseAuto(bool? lightBackground)
    {
        var theme = catalog.Find(ForBackground(lightBackground))!.Theme!;

        return new ThemeChoice(theme, BuiltInThemes.AutoName, $"Theme auto: {theme.Name}.");
    }

    private static string Reasons(IReadOnlyList<string> reasons)
    {
        var shown = string.Join("; ", reasons.Take(MaximumReasons));

        return reasons.Count > MaximumReasons ? $"{shown} (+{reasons.Count - MaximumReasons} more)" : shown;
    }

    private static string? Join(List<string> notices)
    {
        return notices.Count == 0 ? null : $"{string.Join(" | ", notices)}.";
    }

    // The status line is one clipped row, so the listing is by family; a word lists the names inside one.
    private string List(string current)
    {
        var families = catalog.Names
            .GroupBy(Family)
            .Select(group => group.Count() == 1 ? group.First() : group.Key);
        var listing = $"Themes: {string.Join(", ", families)} (:theme <word> lists the names; now {current}).";

        return catalog.Problems.Count == 0 ? listing : $"{listing} Not used: {Reasons(catalog.Problems)}.";
    }

    private string NotFound(string name)
    {
        var word = name.Trim().ToLowerInvariant();
        var matches = catalog.Names.Where(candidate => candidate.Contains(word, StringComparison.Ordinal)).ToList();

        if (matches.Count == 0 || word.Length == 0)
        {
            return $"Theme {name} not found. :theme lists the families.";
        }

        return $"Theme {name} not found. Did you mean: {string.Join(", ", matches)}?";
    }

    private static string Family(string name)
    {
        var index = name.IndexOf(FamilySeparator, StringComparison.Ordinal);

        return index < 0 ? name : name[..index];
    }
}
