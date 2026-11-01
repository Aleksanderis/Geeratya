namespace Geeratya.Core.Profiles;

public enum ProfileLoadStatus
{
    Loaded,
    Missing,

    /// <summary>The file exists but is not valid; a copy was kept next to it as <c>.corrupt</c>.</summary>
    Corrupt,
}
