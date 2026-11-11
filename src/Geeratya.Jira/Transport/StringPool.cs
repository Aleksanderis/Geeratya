namespace Geeratya.Jira.Transport;

/// <summary>
/// Shares equal short strings (status, type, priority and project names, assignees) across the pages and refreshes of
/// one connection, so repeated results do not each hold their own copy. Owned by a <see cref="JiraTransport"/>
/// (never static), thread-safe, and bounded: when it holds <see cref="Capacity"/> entries it is emptied and fills
/// again, which only costs some sharing for a while. Values longer than <see cref="MaxLength"/> are not pooled.
/// </summary>
public sealed class StringPool(int capacity = StringPool.DefaultCapacity)
{
    public const int DefaultCapacity = 4096;
    public const int MaxLength = 128;

    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public int Capacity { get; } = Math.Max(1, capacity);

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>The pooled instance equal to <paramref name="value"/>; null stays null.</summary>
    public string? Share(string? value)
    {
        if (value is null || value.Length > MaxLength)
        {
            return value;
        }

        lock (_lock)
        {
            if (_entries.TryGetValue(value, out var shared))
            {
                return shared;
            }

            if (_entries.Count >= Capacity)
            {
                _entries.Clear();
            }

            _entries[value] = value;

            return value;
        }
    }
}
