namespace Geeratya.Platform.Directories;

/// <summary>The well-known folders of the current user, as the operating system reports them.</summary>
public sealed record PlatformFolders(string Home, string RoamingAppData, string LocalAppData)
{
    /// <summary>Asks the operating system for the folders of the current user.</summary>
    public static PlatformFolders ForCurrentUser()
    {
        return new PlatformFolders(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }
}
