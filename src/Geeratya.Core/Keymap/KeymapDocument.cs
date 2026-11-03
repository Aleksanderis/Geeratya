using System.Text;
using Geeratya.Core.Sources;

namespace Geeratya.Core.Keymap;

/// <summary>Renders the keymap as Markdown for <c>docs/keymap.md</c>.</summary>
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
        builder.Append("do not edit by hand. The Id column is the action id to use in `keymap.json` (`docs/customising-keys.md`). Design rules are in `docs/ux-keymap.md`. ");
        builder.Append("Level: everyday actions show in help, the palette, which-key and the hints by default; ");
        builder.Append("advanced ones show when the command level is advanced (`<space>*` or `:set commandlevel`). ");
        builder.Append("A level hides, it never disables: every key works at either level.\n");

        foreach (var mode in _modes)
        {
            builder.Append($"\n## {mode} mode\n\n| Keys | Action | Id | Level |\n|---|---|---|---|\n");

            foreach (var binding in keymap.Bindings.Where(binding => binding.Mode == mode))
            {
                var title = ActionCatalog.FindTitle(binding.Action);

                builder.Append($"| `{KeyNotation.Display(binding.Keys)}` | {title} | `{binding.Action}` | {LevelWord(binding.Action)} |\n");
            }
        }

        builder.Append("\n## Palette commands\n\n| Command | What it does |\n|---|---|\n");

        foreach (var command in PaletteCommands.All)
        {
            builder.Append($"| `:{command.Usage}` | {command.Description} |\n");
        }

        return builder.ToString();
    }

    private static string LevelWord(string action)
    {
        return ActionCatalog.LevelOf(action) == CommandLevel.Advanced ? "advanced" : "everyday";
    }
}
