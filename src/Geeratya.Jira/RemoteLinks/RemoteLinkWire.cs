namespace Geeratya.Jira.RemoteLinks;

/// <summary>The JSON shapes of <c>GET /issue/{key}/remotelink</c> (a bare array). Every member is optional.</summary>
internal static class RemoteLinkWire
{
    internal sealed record Link(long? Id, Application? Application, Target? Object);

    internal sealed record Application(string? Name);

    internal sealed record Target(string? Url, string? Title);
}
