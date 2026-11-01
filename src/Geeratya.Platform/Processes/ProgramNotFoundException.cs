namespace Geeratya.Platform.Processes;

/// <summary>The requested external program is not installed or not on the path.</summary>
public sealed class ProgramNotFoundException : Exception
{
    public ProgramNotFoundException(string program, Exception innerException)
        : base($"Program '{program}' was not found.", innerException)
    {
    }
}
