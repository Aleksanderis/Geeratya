using Geeratya.Core.Keymap;

namespace Geeratya.Core.State;

/// <summary>
/// The count prefix of Normal mode (<c>5j</c>, <c>3 Alt-j</c>): digits typed before a key. A lone <c>0</c> stays a key
/// (first column), as in vim; after another digit it is part of the count.
/// </summary>
public static class Counts
{
    /// <summary>The largest count; more digits are ignored.</summary>
    public const int Maximum = 999;

    /// <summary>Whether <paramref name="key"/> adds to the count in <paramref name="state"/>.</summary>
    public static bool IsDigit(AppState state, string key)
    {
        if (state.Mode != InputMode.Normal || state.PendingKeys.Length > 0 || key.Length != 1)
        {
            return false;
        }

        return key[0] is >= '1' and <= '9' || (key[0] == '0' && state.Count > 0);
    }

    /// <summary>The count after the digit <paramref name="key"/>.</summary>
    public static int Append(int count, string key)
    {
        var next = (count * 10) + (key[0] - '0');

        return next > Maximum ? count : next;
    }
}
