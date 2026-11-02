using Geeratya.Core.Editing;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Comments;

/// <summary>
/// The suggestion popup of the comment editor (<c>docs/ux-screens.md</c>, "Comment editor"): typing <c>@name</c> or
/// <c>#KEY</c> opens it, it follows the typing, Down/Up (Ctrl-n/Ctrl-p) move, Tab or Enter put the row in as one
/// token (a mention, or a link to the issue; the trigger text is replaced), Esc closes only the popup. Pure; the
/// lookup is the shared <see cref="SearchSuggestionsEffect"/>.
/// </summary>
internal static class EditorSuggestions
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// Updates the popup after <paramref name="result"/> was reduced from <paramref name="before"/>: a changed text
    /// opens or follows it, a trigger that is gone (or a save started) closes it.
    /// </summary>
    public static ReduceResult Follow(AppState before, ReduceResult result)
    {
        if (before.Editor is not { } previous)
        {
            return result;
        }

        if (result.State.Editor is not { } editor)
        {
            return previous.Suggest is null ? result : Closing(result, previous.Tab, previous.Suggest);
        }

        var trigger = EditorTriggers.Find(editor.Buffer);
        var changed = !string.Equals(previous.Buffer.Text, editor.Buffer.Text, StringComparison.Ordinal);

        if (editor.IsSaving || trigger is null)
        {
            return editor.Suggest is null && editor.Dismissed < 0 ? result : Closed(result, editor, -1);
        }

        if (editor.Suggest is { } open && Matches(open, trigger))
        {
            return result;
        }

        if (!changed || trigger.Start == editor.Dismissed)
        {
            return editor.Suggest is null ? result : Closed(result, editor, editor.Dismissed);
        }

        return Opened(result, editor, trigger);
    }

    /// <summary>
    /// The popup's keys: Down/Up move, Tab and Enter accept the highlighted row, Esc closes it. Null when the key is
    /// not the popup's (it goes on to the editor).
    /// </summary>
    public static ReduceResult? Key(AppState state, string? action)
    {
        if (state.Editor is not { Suggest: { } popup } editor)
        {
            return null;
        }

        return action switch
        {
            ActionIds.Cancel => Closed(Result(state), editor, DismissedStart(editor)),
            ActionIds.PickerNext when popup.Items.Count > 0 => Moved(state, editor, popup, 1),
            ActionIds.PickerPrevious when popup.Items.Count > 0 => Moved(state, editor, popup, -1),
            ActionIds.FormNext or ActionIds.FormFieldNext when popup.Selected is { } picked
                => Accepted(state, editor, picked),
            _ => null,
        };
    }

    /// <summary>The lookup answer for the editor's popup; null when the answer is not for it.</summary>
    public static ReduceResult? Reply(AppState state, FormCommand command)
    {
        if (command is MentionsChecked checkedMentions)
        {
            return Flagged(state, checkedMentions);
        }

        if (state.Editor is not { Suggest: { } popup } editor)
        {
            return null;
        }

        SuggestionState? next;

        switch (command)
        {
            case SuggestionsLoaded loaded when loaded.Tab == editor.Tab:
                next = SuggestionPopup.Loaded(popup, loaded.RequestId, loaded.Items, []);

                break;
            case SuggestionsSlow slow when slow.Tab == editor.Tab:
                next = SuggestionPopup.Slow(popup, slow.RequestId);

                break;
            case SuggestionsFailed failed when failed.Tab == editor.Tab:
                next = SuggestionPopup.Failed(popup, failed.RequestId, failed.Message);

                break;
            default:
                return null;
        }

        return Result(next is null ? state : state with { Editor = editor with { Suggest = next } });
    }

    // Only for the editor still open on that comment; a reply for a closed or other editor is dropped.
    private static ReduceResult Flagged(AppState state, MentionsChecked reply)
    {
        if (state.Editor is not { } editor || editor.Tab != reply.Tab || editor.Key != reply.Key
            || editor.CommentId != reply.CommentId)
        {
            return Result(state);
        }

        return Result(state with { Editor = editor with { Unseen = reply.Unseen } });
    }

    private static bool Matches(SuggestionState popup, EditorTrigger trigger)
    {
        return popup.Source.Kind == trigger.Kind && string.Equals(popup.Query, trigger.Query, StringComparison.Ordinal);
    }

    private static ReduceResult Opened(ReduceResult result, CommentEditor editor, EditorTrigger trigger)
    {
        var requestId = result.State.Sequence + 1;
        var source = new SuggestionSource(trigger.Kind, editor.Key, []);
        var previous = editor.Suggest is { } open && open.Source.Kind == trigger.Kind ? open : null;
        var popup = SuggestionPopup.Queried(previous, 0, source, trigger.Query, [], requestId);
        var state = result.State with { Sequence = requestId, Editor = editor with { Suggest = popup, Dismissed = -1 } };
        var effect = new SearchSuggestionsEffect(editor.Tab, requestId, source, trigger.Query, SuggestionPopup.Debounce);

        return new ReduceResult(state, [.. result.Effects, effect]);
    }

    private static ReduceResult Moved(AppState state, CommentEditor editor, SuggestionState popup, int delta)
    {
        var moved = SuggestionPopup.Moved(popup, delta);

        return Result(state with { Editor = editor with { Suggest = moved } });
    }

    private static ReduceResult Accepted(AppState state, CommentEditor editor, Suggestion picked)
    {
        var trigger = EditorTriggers.Find(editor.Buffer);

        if (trigger is null)
        {
            return Closed(Result(state), editor, -1);
        }

        var token = TokenFor(state, trigger, picked);
        var buffer = editor.Buffer;
        var replaced = buffer.Replace(trigger.Start, buffer.Cursor, $"{token}{SpaceAfter(buffer)}");

        return Closed(Result(state), editor with { Buffer = replaced }, -1);
    }

    // Typing goes on after a token: a space, unless whitespace (or the end of the text) follows already.
    private static string SpaceAfter(TextBuffer buffer)
    {
        var followed = buffer.Cursor < buffer.Text.Length && !char.IsWhiteSpace(buffer.Text[buffer.Cursor]);

        return followed ? " " : string.Empty;
    }

    // A mention carries the account id; an issue becomes a link to its page on the active site (plain key without one).
    private static string TokenFor(AppState state, EditorTrigger trigger, Suggestion picked)
    {
        if (trigger.Kind == SuggestionKind.IssueUsers)
        {
            return AdfMarkdownTokens.Mention(picked.Id, picked.Label);
        }

        return state.Session.SiteUrl is { Length: > 0 } site ? AdfMarkdownTokens.IssueLink(site, picked.Id) : picked.Id;
    }

    private static int DismissedStart(CommentEditor editor)
    {
        return EditorTriggers.Find(editor.Buffer)?.Start ?? -1;
    }

    private static ReduceResult Closed(ReduceResult result, CommentEditor editor, int dismissed)
    {
        var closing = editor.Suggest is { RequestId: > 0 } ? Closing(result, editor.Tab, editor.Suggest) : result;
        var state = closing.State with { Editor = editor with { Suggest = null, Dismissed = dismissed } };

        return new ReduceResult(state, closing.Effects);
    }

    private static ReduceResult Closing(ReduceResult result, TabId tab, SuggestionState popup)
    {
        return popup.RequestId == 0
            ? result
            : new ReduceResult(result.State, [.. result.Effects, new CancelSuggestionsEffect(tab)]);
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
