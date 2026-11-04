namespace Geeratya.Platform.Tests.Scripts;

/// <summary>A fact that is skipped when no <c>pwsh</c> executable is on the PATH.</summary>
public sealed class PowerShellAvailableFactAttribute : FactAttribute
{
    public PowerShellAvailableFactAttribute()
    {
        if (PowerShellLocator.Find() is null)
        {
            Skip = "pwsh is not installed on this machine.";
        }
    }
}
