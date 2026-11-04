namespace Geeratya.App.Tests.Fakes;

/// <summary>Reads the recorded, scrubbed replies of the tenant lookup and the GraphQL gateway (#681).</summary>
public static class GatewayFixtures
{
    public static string Read(string name)
    {
        return RecordedFixtures.Read($"gateway/{name}.json");
    }
}
