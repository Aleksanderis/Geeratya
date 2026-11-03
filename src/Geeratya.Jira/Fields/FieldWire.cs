namespace Geeratya.Jira.Fields;

/// <summary>The JSON shape of <c>GET /field</c>. Every member is optional.</summary>
internal static class FieldWire
{
    internal sealed record Field(string? Id, string? Name, Schema? Schema);

    internal sealed record Schema(string? Custom);
}
