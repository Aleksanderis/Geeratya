using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>Adds nodes to a <see cref="JsonArray"/> without the reflection-based generic overload (not AOT safe).</summary>
public static class DemoJsonArrayExtensions
{
    public static void Push(this JsonArray array, JsonNode node) => array.Add(node);
}
