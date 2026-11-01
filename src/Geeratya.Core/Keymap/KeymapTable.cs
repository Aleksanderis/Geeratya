using Geeratya.Core.State;

namespace Geeratya.Core.Keymap;

/// <summary>The key bindings of every mode, resolved key by key; sequences such as <c>g g</c> are supported.</summary>
public sealed class KeymapTable
{
    private const char SequenceSeparator = ' ';

    public KeymapTable(IReadOnlyList<KeyBinding> bindings)
    {
        Bindings = bindings;
    }

    public IReadOnlyList<KeyBinding> Bindings { get; }

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

    /// <summary>Everything wrong with the table: duplicates, shadowed sequences, unknown actions, bad key spelling.</summary>
    public IReadOnlyList<string> FindProblems()
    {
        var problems = new List<string>();

        foreach (var binding in Bindings)
        {
            AddBindingProblems(problems, binding);
        }

        return problems;
    }

    private void AddBindingProblems(List<string> problems, KeyBinding binding)
    {
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

        foreach (var other in Bindings)
        {
            if (ReferenceEquals(other, binding) || other.Mode != binding.Mode)
            {
                continue;
            }

            if (other.Keys == binding.Keys && string.CompareOrdinal(other.Action, binding.Action) < 0)
            {
                problems.Add($"{label}: bound to both {other.Action} and {binding.Action}");
            }

            if (other.Keys.StartsWith($"{binding.Keys}{SequenceSeparator}", StringComparison.Ordinal))
            {
                problems.Add($"{label}: shadows the longer binding {other.Keys}");
            }
        }
    }
}
