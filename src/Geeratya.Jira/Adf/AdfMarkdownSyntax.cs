namespace Geeratya.Jira.Adf;

/// <summary>
/// The tokens of the Markdown-like text an ADF document is edited as. The writer and the parser share them, so what
/// one emits the other reads. Beyond plain Markdown: <c>++text++</c> is underline, a mention is
/// <c>@[Name](mention:accountId)</c> and an inline card is an autolink <c>&lt;https://...&gt;</c>.
/// </summary>
internal static class AdfMarkdownSyntax
{
    public const char Escape = '\\';
    public const char Emphasis = '*';
    public const char StrikeCharacter = '~';
    public const char UnderlineCharacter = '+';
    public const char CodeFence = '`';
    public const char HeadingMarker = '#';
    public const char QuoteMarker = '>';
    public const char RuleMarker = '-';
    public const char LinkTextStart = '[';
    public const char LinkTextEnd = ']';
    public const char DestinationStart = '(';
    public const char DestinationEnd = ')';
    public const char MentionStart = '@';
    public const char AutolinkStart = '<';
    public const char AutolinkEnd = '>';
    public const string Strong = "**";
    public const string Strike = "~~";
    public const string Underline = "++";
    public const string MentionScheme = "mention:";
    public const string Rule = "---";
    public const string BulletMarker = "- ";
    public const string TaskTodoMarker = "[ ] ";
    public const string TaskDoneMarker = "[x] ";
    public const string QuotePrefix = "> ";
    public const int MaxHeadingLevel = 6;
    public const int MinFenceLength = 3;
    public const int TabWidth = 4;

    /// <summary>The longest block nesting the parser opens; deeper lines stay plain text.</summary>
    public const int MaxNesting = AdfParser.MaxDepth;
}
