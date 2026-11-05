using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// The rows of the labels dropdown while its typing buffer is empty: the labels the issue has. They are ordinary
/// suggestions (the popup, its keys and the mouse stay one path); taking one removes that label.
/// </summary>
public static class LabelQuickRows
{
    /// <summary>The detail that marks a label the issue already has (shown as <c>label (remove)</c>).</summary>
    public const string AppliedDetail = "applied";

    public static Suggestion Applied(string label)
    {
        return new Suggestion(label, label, AppliedDetail, false);
    }
}
