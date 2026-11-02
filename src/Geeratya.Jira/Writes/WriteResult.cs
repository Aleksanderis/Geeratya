using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Writes;

/// <summary>The outcome of a Jira write: a value or a typed <see cref="WriteError"/>.</summary>
public sealed class WriteResult<T>
{
    private readonly T? _value;
    private readonly WriteError? _error;

    internal WriteResult(T? value, WriteError? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public T Value => _error is null ? _value! : throw new InvalidOperationException("The write failed.");

    public WriteError Error => _error ?? throw new InvalidOperationException("The write succeeded.");
}
