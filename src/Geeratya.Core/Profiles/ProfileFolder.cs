using System.Security.Cryptography;
using System.Text;

namespace Geeratya.Core.Profiles;

/// <summary>The folder name of a profile's local data: a hash of its credential key, so no site or e-mail shows.</summary>
public static class ProfileFolder
{
    private const int HashLength = 16;

    public static string NameOf(Profile profile)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(profile.CredentialKey));

        return Convert.ToHexStringLower(hash)[..HashLength];
    }
}
