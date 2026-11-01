namespace Geeratya.Jira.Transport;

/// <summary>The outcome of a Jira call: a value or a typed error, never an <c>HttpRequestException</c>.</summary>
public sealed class JiraResult<T>
{
    private readonly T? _value;
    private readonly JiraError? _error;

    internal JiraResult(T? value, JiraError? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public T Value => _error is null ? _value! : throw new InvalidOperationException("The call failed.");

    public JiraError Error => _error ?? throw new InvalidOperationException("The call succeeded.");
}
