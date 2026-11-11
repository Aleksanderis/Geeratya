using System.Diagnostics.CodeAnalysis;

namespace Geeratya.Platform.Secrets;

/// <summary>
/// Maps a Win32 error code of a Credential Manager call to the store's behaviour: "not found" is an ordinary answer
/// (no token saved), every other code is a failure the user must see. Pure, so it is tested on every system.
/// </summary>
public static class CredentialManagerErrors
{
    /// <summary>ERROR_NOT_FOUND: there is no credential with that target name.</summary>
    public const int ErrorNotFound = 1168;

    /// <summary>ERROR_ACCESS_DENIED.</summary>
    public const int ErrorAccessDenied = 5;

    /// <summary>ERROR_NO_SUCH_LOGON_SESSION: the vault is not available for this logon.</summary>
    public const int ErrorNoSuchLogonSession = 1312;

    /// <summary>ERROR_INVALID_FLAGS, returned by some broken vault states.</summary>
    public const int ErrorInvalidFlags = 1004;

    /// <summary>
    /// Does nothing for <see cref="ErrorNotFound"/>; throws <see cref="SecretStoreException"/> otherwise.
    /// </summary>
    public static void ThrowUnlessNotFound(int error, string action)
    {
        if (error != ErrorNotFound)
        {
            Throw(error, action);
        }
    }

    /// <summary>
    /// Throws a <see cref="SecretStoreException"/> that names the action and the error, never a secret.
    /// </summary>
    [DoesNotReturn]
    public static void Throw(int error, string action)
    {
        throw new SecretStoreException(Describe(error, action));
    }

    /// <summary>The message for an error code; it contains the action and the code only.</summary>
    public static string Describe(int error, string action)
    {
        var reason = error switch
        {
            ErrorAccessDenied => "access denied",
            ErrorNoSuchLogonSession => "no logon session for the vault",
            ErrorInvalidFlags => "invalid flags",
            _ => "unexpected failure",
        };

        return $"Credential Manager could not {action} the token ({reason}, error {error}).";
    }
}
