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
        builder.Append("do not edit by hand. Design rules are in `docs/ux-keymap.md`.\n");

        foreach (var mode in _modes)
        {
            builder.Append($"\n## {mode} mode\n\n| Keys | Action |\n|---|---|\n");

            foreach (var binding in keymap.Bindings.Where(binding => binding.Mode == mode))
            {
                builder.Append($"| `{binding.Keys}` | {ActionCatalog.FindTitle(binding.Action)} |\n");
            }
        }

        builder.Append("\n## Palette commands\n\n| Command | What it does |\n|---|---|\n");

        foreach (var command in PaletteCommands.All)
        {
            builder.Append($"| `:{command.Usage}` | {command.Description} |\n");
        }

        return builder.ToString();
    }

    /// <summary>The help overlay text: one line per binding, grouped by mode.</summary>
    public static IReadOnlyList<string> RenderHelpLines(KeymapTable keymap)
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
                lines.Add($"  {binding.Keys,-10} {ActionCatalog.FindTitle(binding.Action)}");
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
}
