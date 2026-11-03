namespace Geeratya.Core.Startup;

/// <summary>Where the start of a session is: the remembered tabs are read, then the first view is chosen.</summary>
public enum StartupPhase
{
    /// <summary>The start did not begin (no profile, or a session built by hand); the tabs are not saved.</summary>
    Idle,

    /// <summary>The remembered tabs are being read; the tabs are not saved yet, so the file is not overwritten.</summary>
    Loading,

    /// <summary>Nothing was restored; the board picker (or the only board) opens once the connection works.</summary>
    AwaitingConnection,

    /// <summary>The start is over; changes to the tabs are saved.</summary>
    Done,
}
