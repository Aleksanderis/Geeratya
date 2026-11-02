namespace Geeratya.Core.Writes;

/// <summary>Where a running operation is. A finished operation leaves the list; there is no done phase.</summary>
public enum OperationPhase
{
    /// <summary>A transition id or form metadata is being fetched (a read).</summary>
    Resolving,

    /// <summary>The write is on the wire.</summary>
    Sending,

    /// <summary>The outcome is unknown and the issue is being read again.</summary>
    Verifying,
}
