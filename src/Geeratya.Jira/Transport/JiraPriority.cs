namespace Geeratya.Jira.Transport;

/// <summary>Who asked for a request. User requests are served before background ones and may use every slot.</summary>
public enum JiraPriority
{
    /// <summary>The user is waiting for the answer (a key press, opening an issue, a write).</summary>
    User,

    /// <summary>Refresh or prefetch nobody waits for. Yields to <see cref="User"/> and is throttled earlier.</summary>
    Background,
}
