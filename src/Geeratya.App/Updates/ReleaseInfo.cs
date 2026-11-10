namespace Geeratya.App.Updates;

/// <summary>One file attached to a release.</summary>
public sealed record ReleaseAsset(string Name, string Url);

/// <summary>A published release: its tag, its version (the tag without the <c>v</c>) and its files.</summary>
public sealed record ReleaseInfo(string Tag, string Version, IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>The first asset whose name ends with <paramref name="suffix"/>, or null.</summary>
    public ReleaseAsset? FindBySuffix(string suffix)
    {
        return Assets.FirstOrDefault(asset => asset.Name.EndsWith(suffix, StringComparison.Ordinal));
    }
}

/// <summary>The answer of asking for the latest release: the release, or why there is none to show.</summary>
public sealed record ReleaseLookup(ReleaseInfo? Release, string? Error)
{
    public static ReleaseLookup Found(ReleaseInfo release)
    {
        return new ReleaseLookup(release, null);
    }

    public static ReleaseLookup Failed(string error)
    {
        return new ReleaseLookup(null, error);
    }
}
