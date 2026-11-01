using System.Text.Json;

namespace Geeratya.Core.Keymap;

/// <summary>Loads the keymap shipped inside the assembly (<c>keymap.default.json</c>).</summary>
public static class DefaultKeymap
{
    private const string ResourceName = "keymap.default.json";

    public static KeymapTable Load()
    {
        using var stream = typeof(DefaultKeymap).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        var file = JsonSerializer.Deserialize(stream, KeymapJsonContext.Default.KeymapFile)
            ?? throw new InvalidOperationException($"{ResourceName} is empty.");

        return new KeymapTable(file.Bindings);
    }
}
