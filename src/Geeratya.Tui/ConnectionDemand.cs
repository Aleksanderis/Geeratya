using Geeratya.Core.Profiles;

namespace Geeratya.Tui;

/// <summary>
/// Remembers which profile (and profile count) the connection was last asked for, so the host asks again only
/// when one of them changed and not on every key, mouse or hover event (#933).
/// </summary>
public sealed class ConnectionDemand
{
    private const int NothingAsked = -1;

    private Profile? _profile;
    private int _count = NothingAsked;

    /// <summary>True when the profile or the count differs from the last call, or when <paramref name="force"/> is set.</summary>
    public bool Needed(Profile? profile, int count, bool force)
    {
        if (!force && count == _count && profile == _profile)
        {
            return false;
        }

        _profile = profile;
        _count = count;

        return true;
    }
}
