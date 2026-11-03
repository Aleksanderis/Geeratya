using System.Text;
using Geeratya.Core.Sources;

namespace Geeratya.Core.Keymap;

/// <summary>Renders the keymap as Markdown for <c>docs/keymap.md</c> and as help lines for the help overlay.</summary>
public static class KeymapDocument
{
    private static readonly InputMode[] _modes =
        [InputMode.Normal, InputMode.Help, InputMode.Command, InputMode.Confirm, InputMode.Insert];

    /// <summary>The generated key reference; lines end with <c>\n</c>.</summary>
    public static string RenderMarkdown(KeymapTable keymap)
    {
        var builder = new StringBuilder();

        builder.Append("# Key reference\n\n");
        builder.Append("Generated from `src/Geeratya.Core/Keymap/keymap.default.json` ");
        builder.Append("by `scripts/gen-keymap-doc.sh`; ");
        builder.Append("do not edit by hand. Design rules are in `docs/ux-keymap.md`. ");
        builder.Append("Level: everyday actions show in help, the palette, which-key and the hints by default; ");
        builder.Append("advanced ones show when the command level is advanced (`<space>*` or `:set commandlevel`). ");
        builder.Append("A level hides, it never disables: every key works at either level.\n");

        foreach (var mode in _modes)
        {
            builder.Append($"\n## {mode} mode\n\n| Keys | Action | Level |\n|---|---|---|\n");

            foreach (var binding in keymap.Bindings.Where(binding => binding.Mode == mode))
            {
                builder.Append($"| `{binding.Keys}` | {ActionCatalog.FindTitle(binding.Action)} | {LevelWord(binding.Action)} |\n");
            }
        }

        builder.Append("\n## Palette commands\n\n| Command | What it does |\n|---|---|\n");

        foreach (var command in PaletteCommands.All)
        {
            builder.Append($"| `:{command.Usage}` | {command.Description} |\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// The help overlay text: one line per binding, grouped by mode. Bindings of actions above
    /// <paramref name="level"/> are left out and a line under the Normal mode section counts them.
    /// </summary>
    public static IReadOnlyList<string> RenderHelpLines(
        KeymapTable keymap,
        CommandLevel level = CommandLevel.Everyday)
    {
        var lines = new List<string>();

        foreach (var mode in _modes)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"{mode} mode");

            foreach (var binding in keymap.Bindings.Where(binding => binding.Mode == mode))
            {
                if (ActionCatalog.IsShownAt(binding.Action, level))
                {
                    lines.Add($"  {binding.Keys,-10} {ActionCatalog.FindTitle(binding.Action)}");
                }
            }

            if (mode == InputMode.Normal && HiddenNote(keymap, level) is { } note)
            {
                lines.Add(note);
            }
        }

        lines.Add(string.Empty);
        lines.Add("Palette commands");

        foreach (var command in PaletteCommands.All)
        {
            lines.Add($"  :{command.Usage}");
            lines.Add($"      {command.Description}");
        }

        return lines;
    }

    /// <summary>
    /// The count of Normal mode bindings the level hides, as one help line naming the toggle key; null when none are
    /// hidden. The key is read from the keymap.
    /// </summary>
    public static string? HiddenNote(KeymapTable keymap, CommandLevel level)
    {
        var hidden = keymap.Bindings
            .Where(binding => binding.Mode == InputMode.Normal && !ActionCatalog.IsShownAt(binding.Action, level))
            .Select(binding => binding.Action)
            .Distinct()
            .Count();

        if (hidden == 0)
        {
            return null;
        }

        var noun = hidden == 1 ? "advanced command" : "advanced commands";
        var toggle = keymap.FindKeys(InputMode.Help, ActionIds.ViewCycleCommandLevel);
        var how = toggle is null ? "set commandlevel advanced shows them" : $"{toggle} shows them";

        return $"  ({hidden} {noun} hidden; {how})";
    }

    private static string LevelWord(string action)
    {
        return ActionCatalog.LevelOf(action) == CommandLevel.Advanced ? "advanced" : "everyday";
    }
}
