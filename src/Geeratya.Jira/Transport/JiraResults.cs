namespace Geeratya.Jira.Transport;

/// <summary>Creates <see cref="JiraResult{T}"/> values.</summary>
public static class JiraResults
{
    public static JiraResult<T> Success<T>(T value) => new(value, null);

    public static JiraResult<T> Failure<T>(JiraError error) => new(default, error);
}
