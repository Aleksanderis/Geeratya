namespace Geeratya.App;

/// <summary>Creates the services for the running process (<see cref="ProfileServices.CreateForProcess"/>).</summary>
public sealed class ProcessProfileServicesFactory : IProfileServicesFactory
{
    public ProfileServices Create()
    {
        return ProfileServices.CreateForProcess();
    }
}
