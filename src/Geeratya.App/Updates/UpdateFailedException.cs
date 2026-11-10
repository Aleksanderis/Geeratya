namespace Geeratya.App.Updates;

/// <summary>An update that could not finish; the message is for the user, the installed binary is untouched.</summary>
public sealed class UpdateFailedException : Exception
{
    public UpdateFailedException(string message)
        : base(message)
    {
    }

    public UpdateFailedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
