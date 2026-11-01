using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Secrets;

/// <summary>Picks the secret store for an operating system; every supported system has a branch.</summary>
public static class SecretStores
{
    public static ISecretStore Create(OperatingSystemKind operatingSystem, IProcessRunner processRunner)
    {
        return operatingSystem switch
        {
            OperatingSystemKind.Windows when OperatingSystem.IsWindows() => new WindowsSecretStore(),
            OperatingSystemKind.Linux => new LinuxSecretStore(processRunner),
            _ => new UnsupportedSecretStore(),
        };
    }
}
