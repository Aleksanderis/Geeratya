namespace Geeratya.Core.Adf;

/// <summary>Literal text the renderer adds around ADF content.</summary>
internal static class AdfMarkers
{
    public const string BulletMarker = "- ";
    public const string QuoteBar = "| ";
    public const string TaskDone = "[x] ";
    public const string TaskTodo = "[ ] ";
    public const string ExpandOpen = "v ";
    public const string ExpandIndent = "  ";
    public const string Fence = "```";
    public const string Image = "[image]";
    public const string File = "[file]";
    public const string Link = "[link]";
    public const string DefaultDecisionLabel = "decision";
    public const string DefaultExpandTitle = "Expand";
    public const string Truncated = "[unsupported: nesting too deep]";
    public const char RuleCharacter = '-';
    public const int MaxRuleLength = 40;
    public const int MaxStateLength = 20;

    public static string Unsupported(string typeName)
    {
        return $"[unsupported: {typeName}]";
    }
}
