namespace Geeratya.Core.State;

/// <summary>A profile operation did not complete; the catalog in the state stays as it was.</summary>
public sealed record ProfileOperationFailed(string Message) : Command;
