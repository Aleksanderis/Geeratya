namespace Geeratya.Jira.Adf;

/// <summary>The toggled text styles of the Markdown-like text (code and link are not toggles).</summary>
[Flags]
public enum AdfMarkdownStyle
{
    None = 0,
    Strong = 1,
    Emphasis = 2,
    Strike = 4,
    Underline = 8,
}
