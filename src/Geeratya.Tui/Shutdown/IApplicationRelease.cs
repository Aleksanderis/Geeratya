namespace Geeratya.Tui.Shutdown;

/// <summary>Disposes the running application, which restores the terminal; a seam so the exit order can be tested.</summary>
public interface IApplicationRelease
{
    /// <summary>Detaches and disposes the application; does nothing when it is already gone.</summary>
    void Release();
}
