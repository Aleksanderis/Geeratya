using System.Reflection;

namespace Geeratya.Core;

public static class ApplicationInfo
{
    public const string Name = "geeratya";

    /// <summary>The product version from the build (<c>Version</c> in Directory.Build.props).</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var attribute = typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return attribute?.InformationalVersion ?? "0.0.0";
    }
}
