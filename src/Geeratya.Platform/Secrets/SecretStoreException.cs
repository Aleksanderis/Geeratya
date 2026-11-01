namespace Geeratya.Platform.Secrets;

/// <summary>The secret store failed. The message never contains the secret.</summary>
public sealed class SecretStoreException : Exception
{
    public SecretStoreException(string message)
        : base(message)
    {
    }
}
