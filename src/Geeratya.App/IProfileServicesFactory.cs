namespace Geeratya.App;

/// <summary>
/// Creates the <see cref="ProfileServices"/> when the shell needs them, so a start that only prints help or a
/// version touches no folder.
/// </summary>
public interface IProfileServicesFactory
{
    /// <summary>
    /// Wires the services; throws <see cref="InvalidOperationException"/> with a readable message when no home
    /// directory can be found.
    /// </summary>
    ProfileServices Create();
}
