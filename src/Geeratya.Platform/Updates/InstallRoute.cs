namespace Geeratya.Platform.Updates;

/// <summary>How an update reaches an install: by the binary itself, or by something else that owns the files.</summary>
public enum InstallRouteKind
{
    /// <summary>Put there by an install script: <c>geeratya update</c> replaces the binary.</summary>
    Script,

    /// <summary>A package manager owns the files; <c>geeratya update</c> only says which command to run.</summary>
    PackageManager,

    /// <summary>Not recognised (a portable archive, a build from source): update by hand.</summary>
    Manual,
}

/// <summary>The detected route and what the user does for an update when it is not <c>geeratya update</c>.</summary>
/// <param name="Kind">Who replaces the files.</param>
/// <param name="Instructions">A sentence fragment naming the command or steps ("run `brew upgrade geeratya`").</param>
public sealed record InstallRoute(InstallRouteKind Kind, string Instructions)
{
    /// <summary>The instructions of a script install: the command is the whole story.</summary>
    public const string UpdateCommandText = "run `geeratya update`";
}
