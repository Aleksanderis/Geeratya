namespace Geeratya.Platform;

/// <summary>
/// Reads environment variables through a lookup that defaults to the process environment; tests inject one so they
/// never touch the process.
/// </summary>
public sealed class EnvironmentReader
{
    private readonly Func<string, string?> _lookup;

    public EnvironmentReader(Func<string, string?>? lookup = null)
    {
        _lookup = lookup ?? Environment.GetEnvironmentVariable;
    }

    /// <summary>Creates a reader over the environment of the running process.</summary>
    public static EnvironmentReader ForProcess()
    {
        return new EnvironmentReader();
    }

    /// <summary>Returns the value, or null when the variable is missing or empty.</summary>
    public string? Get(string name)
    {
        var value = _lookup(name);

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
