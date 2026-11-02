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

    public void Replace(IReadOnlyList<KeyBinding> bindings)
    {
        Bindings = bindings;
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
