using Geeratya.Core.Views;

namespace Geeratya.Core.Cache;

/// <summary>
/// What a cached view is filed under inside one profile's cache: the source kind and its value (the profile is the
/// cache's own scope, not part of the key).
/// </summary>
public sealed record CacheKey(ViewSourceKind Source, string SourceValue)
{
    /// <summary>The key of the view a definition describes; the layout and title are not part of it.</summary>
    public static CacheKey For(ViewDefinition definition)
    {
        return new CacheKey(definition.Source, definition.SourceValue);
    }
}
