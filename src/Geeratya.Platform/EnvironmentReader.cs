namespace Geeratya.Platform;

/// <summary>Reads environment variables through an injected lookup, so tests never touch the process environment.</summary>
public sealed class EnvironmentReader
{
    private readonly Func<string, string?> _lookup;

    public EnvironmentReader(Func<string, string?> lookup)
    {
        _lookup = lookup;
    }

    /// <summary>Creates a reader over the environment of the running process.</summary>
    public static EnvironmentReader ForProcess()
    {
        return new EnvironmentReader(Environment.GetEnvironmentVariable);
    }

    /// <summary>Returns the value, or null when the variable is missing or empty.</summary>
    public string? Get(string name)
    {
        var value = _lookup(name);

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
