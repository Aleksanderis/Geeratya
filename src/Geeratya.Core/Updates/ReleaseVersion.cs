using System.Globalization;

namespace Geeratya.Core.Updates;

/// <summary>
/// A release version as the tags spell it (<c>1.2.3</c>, <c>v1.2.3</c>, <c>1.2.3-rc.1</c>), ordered by the semantic
/// versioning rules: the numbers first, a pre-release before its release, pre-release parts one by one (numbers
/// as numbers, a number before a word). Build metadata (<c>+...</c>) is ignored.
/// </summary>
public sealed class ReleaseVersion
{
    /// <summary>The version of a build that did not get one from a release tag.</summary>
    public const string DevelopmentText = "0.0.0-dev";

    private readonly string[] _preRelease;

    private ReleaseVersion(int major, int minor, int patch, string[] preRelease, string text)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
        Text = text;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>The version without a leading <c>v</c> and without build metadata.</summary>
    public string Text { get; }

    /// <summary>True for the marker of a build made without a release tag (<see cref="DevelopmentText"/>).</summary>
    public bool IsDevelopment => Text == DevelopmentText;

    /// <summary>Reads a version; null when <paramref name="text"/> is not one.</summary>
    public static ReleaseVersion? Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        var plus = trimmed.IndexOf('+', StringComparison.Ordinal);

        if (plus >= 0)
        {
            trimmed = trimmed[..plus];
        }

        var dash = trimmed.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? trimmed : trimmed[..dash];
        var pre = dash < 0 ? string.Empty : trimmed[(dash + 1)..];
        var numbers = core.Split('.');

        if (numbers.Length != 3 || (dash >= 0 && pre.Length == 0))
        {
            return null;
        }

        if (!TryNumber(numbers[0], out var major) || !TryNumber(numbers[1], out var minor)
            || !TryNumber(numbers[2], out var patch))
        {
            return null;
        }

        if (pre.Length > 0 && pre.Split('.').Any(part => part.Length == 0))
        {
            return null;
        }

        var parts = pre.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return new ReleaseVersion(major, minor, patch, parts, trimmed);
    }

    /// <summary>
    /// Negative when this version is earlier than <paramref name="other"/>, positive when later, 0 when equal.
    /// </summary>
    public int CompareTo(ReleaseVersion other)
    {
        var core = Major.CompareTo(other.Major);

        core = core != 0 ? core : Minor.CompareTo(other.Minor);
        core = core != 0 ? core : Patch.CompareTo(other.Patch);

        return core != 0 ? core : ComparePreRelease(_preRelease, other._preRelease);
    }

    /// <summary>True when this version is a later release than <paramref name="other"/>.</summary>
    public bool IsNewerThan(ReleaseVersion other)
    {
        return CompareTo(other) > 0;
    }

    public override string ToString()
    {
        return Text;
    }

    private static int ComparePreRelease(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            // A release is later than any pre-release of the same numbers.
            return right.Length.CompareTo(left.Length);
        }

        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var compared = ComparePart(left[index], right[index]);

            if (compared != 0)
            {
                return compared;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int ComparePart(string left, string right)
    {
        var leftNumeric = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightNumeric = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftNumeric != rightNumeric)
        {
            return leftNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool TryNumber(string text, out int number)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
