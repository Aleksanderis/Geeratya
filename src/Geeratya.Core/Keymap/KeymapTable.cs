namespace Geeratya.Core.Keymap;

/// <summary>The key bindings of every mode, resolved key by key; sequences such as <c>g g</c> are supported.</summary>
public sealed class KeymapTable
{
    private const char SequenceSeparator = ' ';

    public KeymapTable(IReadOnlyList<KeyBinding> bindings)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// The current bindings. <see cref="Replace"/> swaps them for <c>:keys reload</c>; the shell and the reducer share
    /// one table and use it on the UI thread only.
    /// </summary>
    public IReadOnlyList<KeyBinding> Bindings { get; private set; }

    /// <summary>The changes the user's <c>keymap.json</c> made to the defaults; empty without a file.</summary>
    public IReadOnlyList<CustomKeymapRow> Custom { get; private set; } = [];

    public void Replace(IReadOnlyList<KeyBinding> bindings, IReadOnlyList<CustomKeymapRow>? custom = null)
    {
        Bindings = bindings;
        Custom = custom ?? [];
    }

    /// <summary>Feeds <paramref name="key"/> after the already typed <paramref name="pending"/> keys.</summary>
    public KeyResolution Resolve(InputMode mode, string pending, string key)
    {
        var sequence = pending.Length == 0 ? key : $"{pending}{SequenceSeparator}{key}";
        var isPrefix = false;

        foreach (var binding in Bindings)
        {
            if (binding.Mode != mode)
            {
                continue;
            }

            if (binding.Keys == sequence)
            {
                return new KeyResolution(KeyResolutionKind.Action, binding.Action);
            }

            if (binding.Keys.StartsWith($"{sequence}{SequenceSeparator}", StringComparison.Ordinal))
            {
                isPrefix = true;
            }
        }

        return new KeyResolution(isPrefix ? KeyResolutionKind.Prefix : KeyResolutionKind.None, null);
    }

    /// <summary>
    /// The keys that continue <paramref name="pending"/> in <paramref name="mode"/>, in keymap order, one entry per
    /// next key (the data of the which-key popup); empty when nothing continues it. Actions above
    /// <paramref name="level"/>
    /// are left out, and a group with no action left disappears with them.
    /// </summary>
    public IReadOnlyList<NextKey> NextKeys(
        InputMode mode,
        string pending,
        CommandLevel level = CommandLevel.Advanced)
    {
        var prefix = $"{pending}{SequenceSeparator}";
        var order = new List<string>();
        var actions = new Dictionary<string, string?>();
        var counts = new Dictionary<string, int>();

        foreach (var binding in Bindings)
        {
            if (binding.Mode != mode
                || !binding.Keys.StartsWith(prefix, StringComparison.Ordinal)
                || !ActionCatalog.IsShownAt(binding.Action, level))
            {
                continue;
            }

            var rest = binding.Keys[prefix.Length..];
            var separator = rest.IndexOf(SequenceSeparator);
            var key = separator < 0 ? rest : rest[..separator];

            if (!counts.TryAdd(key, 1))
            {
                counts[key]++;
            }
            else
            {
                order.Add(key);
            }

            actions[key] = separator < 0 ? binding.Action : null;
        }

        return order.Select(key => new NextKey(key, actions[key], counts[key])).ToList();
    }

    /// <summary>The first keys bound to <paramref name="action"/> in <paramref name="mode"/>, or null when unbound.</summary>
    public string? FindKeys(InputMode mode, string action)
    {
        foreach (var binding in Bindings)
        {
            if (binding.Mode == mode && binding.Action == action)
            {
                return binding.Keys;
            }
        }

        return null;
    }

    /// <summary>Everything wrong: duplicates, shadowed sequences, unknown actions, bad key spelling.</summary>
    public IReadOnlyList<string> FindProblems()
    {
        var problems = new List<string>();

        for (var index = 0; index < Bindings.Count; index++)
        {
            AddBindingProblems(problems, index);
        }

        return problems;
    }

    private void AddBindingProblems(List<string> problems, int index)
    {
        var binding = Bindings[index];
        var label = $"{binding.Mode} {binding.Keys}";

        if (ActionCatalog.FindTitle(binding.Action) is null)
        {
            problems.Add($"{label}: unknown action {binding.Action}");
        }

        foreach (var key in binding.Keys.Split(SequenceSeparator))
        {
            if (!KeyNotation.IsValid(key))
            {
                problems.Add($"{label}: bad key spelling {key}");
            }
        }

        for (var otherIndex = 0; otherIndex < Bindings.Count; otherIndex++)
        {
            var other = Bindings[otherIndex];

            if (otherIndex == index || other.Mode != binding.Mode)
            {
                continue;
            }

            if (other.Keys == binding.Keys && otherIndex < index)
            {
                problems.Add($"{label}: bound twice ({other.Action}, {binding.Action})");
            }

            if (other.Keys.StartsWith($"{binding.Keys}{SequenceSeparator}", StringComparison.Ordinal))
            {
                problems.Add($"{label}: shadows the longer binding {other.Keys}");
            }
        }
    }
}
