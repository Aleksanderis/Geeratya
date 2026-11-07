using System.Reflection;

namespace Geeratya.Core;

public static class ApplicationInfo
{
    public const string Name = "geeratya";

    /// <summary>The product version from the build (<c>0.0.0-dev</c> unless a release build passes <c>-p:Version</c>).</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var attribute = typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return attribute?.InformationalVersion ?? "0.0.0-dev";
    }
}
