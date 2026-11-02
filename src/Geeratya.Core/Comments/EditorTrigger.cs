using Geeratya.Core.Suggestions;

namespace Geeratya.Core.Comments;

/// <summary>A trigger character with the typing after it, ending at the cursor of the comment editor.</summary>
/// <param name="Kind"><see cref="SuggestionKind.IssueUsers"/> for <c>@</c>, <see cref="SuggestionKind.Issues"/> for <c>#</c>.</param>
/// <param name="Start">The text index of the trigger character.</param>
/// <param name="Query">The typing after the trigger character (the <c>#</c> and <c>@</c> are not part of it).</param>
public sealed record EditorTrigger(SuggestionKind Kind, int Start, string Query);
