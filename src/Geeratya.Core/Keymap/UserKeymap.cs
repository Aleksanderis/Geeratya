using System.Text.Json;

namespace Geeratya.Core.Keymap;

/// <summary>
/// Merges the user's <c>keymap.json</c> over the default bindings. Never throws for bad content: a row that is wrong
/// is skipped and reported, a file that cannot be parsed leaves the defaults.
/// </summary>
public static class UserKeymap
{
    private const char SequenceSeparator = ' ';

    public static KeymapLoadResult Merge(IReadOnlyList<KeyBinding> defaults, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new KeymapLoadResult(defaults, []);
        }

        UserKeymapFile? file;

        try
        {
            file = JsonSerializer.Deserialize(json, KeymapJsonContext.Default.UserKeymapFile);
        }
        catch (JsonException exception)
        {
            return Unreadable(defaults, exception.Message);
        }

        return file is null ? Unreadable(defaults, "the file is empty") : Merge(defaults, file);
    }

    private static KeymapLoadResult Unreadable(IReadOnlyList<KeyBinding> defaults, string reason)
    {
        return new KeymapLoadResult(defaults, [$"not valid, defaults are in use ({reason})"]);
    }

    private static KeymapLoadResult Merge(IReadOnlyList<KeyBinding> defaults, UserKeymapFile file)
    {
        var problems = new List<string>();
        var remaining = defaults.ToList();
        var custom = new List<CustomKeymapRow>();

        foreach (var row in file.Unbind ?? [])
        {
            if (row is not { Mode: not null, Keys: not null })
            {
                problems.Add($"unbind {Label(row?.Mode, row?.Keys)}: a row needs mode and keys");
            }
            else if (remaining.RemoveAll(binding => binding.Mode == row.Mode && binding.Keys == row.Keys) == 0)
            {
                problems.Add($"unbind {Label(row.Mode, row.Keys)}: no default binding with these keys");
            }
            else
            {
                custom.Add(new CustomKeymapRow(row.Mode.Value, row.Keys, null));
            }
        }

        var added = new List<KeyBinding>();

        foreach (var userRow in file.Bindings ?? [])
        {
            var problem = FindRowProblem(userRow);

            if (problem is not null)
            {
                problems.Add(problem);

                continue;
            }

            var row = new KeyBinding(userRow!.Mode!.Value, userRow.Keys!, userRow.Action!);

            remaining.RemoveAll(binding => binding.Mode == row.Mode && binding.Keys == row.Keys);

            var conflict = FindConflict(row, added.Concat(remaining));

            if (conflict is not null)
            {
                problems.Add(conflict);

                continue;
            }

            added.Add(row);
            custom.Add(new CustomKeymapRow(row.Mode, row.Keys, row.Action));
        }

        // The user's rows come first, so a hint or help line shows the key the user chose for an action.
        return new KeymapLoadResult(added.Concat(remaining).ToList(), problems) { Custom = custom };
    }

    private static string? FindRowProblem(UserKeymapRow? row)
    {
        var label = Label(row?.Mode, row?.Keys);

        if (row is not { Mode: not null, Keys: not null, Action: not null })
        {
            return $"{label}: a row needs mode, keys and action";
        }

        if (ActionCatalog.FindTitle(row.Action) is null)
        {
            return $"{label}: unknown action {row.Action}";
        }

        foreach (var key in row.Keys.Split(SequenceSeparator))
        {
            if (!KeyNotation.IsValid(key))
            {
                return $"{label}: bad key spelling '{key}'";
            }

            if (row.Mode is InputMode.Command or InputMode.Insert && KeyNotation.ToText(key) is not null)
            {
                return $"{label}: printable keys are text in {row.Mode} mode and cannot be bound";
            }
        }

        if (row.Mode == InputMode.Normal && ActionCatalog.IsModalOnly(row.Action))
        {
            return $"{label}: {row.Action} only works inside a dialog and cannot be bound in Normal mode";
        }

        return null;
    }

    private static string? FindConflict(KeyBinding row, IEnumerable<KeyBinding> others)
    {
        foreach (var other in others)
        {
            if (other.Mode != row.Mode)
            {
                continue;
            }

            if (other.Keys == row.Keys)
            {
                return $"{Label(row.Mode, row.Keys)}: already bound to {other.Action} by this file";
            }

            if (other.Keys.StartsWith($"{row.Keys}{SequenceSeparator}", StringComparison.Ordinal))
            {
                return $"{Label(row.Mode, row.Keys)}: would shadow {other.Keys} ({other.Action}); unbind that first";
            }

            if (row.Keys.StartsWith($"{other.Keys}{SequenceSeparator}", StringComparison.Ordinal))
            {
                return $"{Label(row.Mode, row.Keys)}: {other.Keys} ({other.Action}) is already bound and would shadow it; unbind that first";
            }
        }

        return null;
    }

    private static string Label(InputMode? mode, string? keys)
    {
        return $"{mode?.ToString().ToLowerInvariant()} {keys}".Trim();
    }
}
