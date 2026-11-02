using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Jump;

/// <summary>
/// The jump prompt (<c>issue.jump</c>): type an issue key, choose a completion, open the issue. Read-only and pure.
/// The issue opens on top of the active tab's issue stack (Esc goes back to where the user was); with no tab open a
/// tab for <c>key = KEY</c> is opened first, so the issue has a view underneath.
/// </summary>
public static class JumpReducer
{
    private const string NoProfileMessage = "No profile yet: add one with :profile add.";
    private const string StackFullMessage = "Too many issues open: press Esc to go back.";
    private const string EmptyNotice = "Type an issue key, like GCM-12, or press Esc.";
    private const string NeedsProjectNotice = "No project to complete the number with: type the full key, like GCM-12.";
    private const string JqlKeyField = "key";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Open(AppState state)
    {
        if (state.Profiles.Active is null)
        {
            return Plain(state with { Message = NoProfileMessage });
        }

        var prompt = new JumpState(string.Empty, 0, null);

        return Plain(Close(state) with { Mode = InputMode.Insert, Jump = prompt, Message = null });
    }

    public static AppState Type(AppState state, string text)
    {
        return state.Jump is { } jump ? state with { Jump = new JumpState($"{jump.Text}{text}", 0, null) } : state;
    }

    public static AppState Backspace(AppState state)
    {
        if (state.Jump is not { Text.Length: > 0 } jump)
        {
            return state;
        }

        var starts = StringInfo.ParseCombiningCharacters(jump.Text);

        return state with { Jump = new JumpState(jump.Text[..starts[^1]], 0, null) };
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, stopping at the first and last row.</summary>
    public static AppState Move(AppState state, int delta)
    {
        if (state.Jump is not { } jump)
        {
            return state;
        }

        var last = Math.Max(0, JumpRows.For(state, jump).Count - 1);
        var current = Math.Clamp(jump.Selected, 0, last);

        return state with { Jump = jump with { Selected = Math.Clamp(current + delta, 0, last), Notice = null } };
    }

    /// <summary>Enter: opens the highlighted row; with no row the notice says why nothing opened.</summary>
    public static ReduceResult Accept(AppState state)
    {
        if (state.Jump is not { } jump)
        {
            return Plain(state);
        }

        var rows = JumpRows.For(state, jump);

        if (rows.Count == 0)
        {
            return Plain(state with { Jump = jump with { Notice = NoRowsNotice(state, jump) } });
        }

        return OpenKey(Close(state), rows[Math.Clamp(jump.Selected, 0, rows.Count - 1)].Key);
    }

    /// <summary>Closes the prompt and leaves Insert mode.</summary>
    public static AppState Close(AppState state)
    {
        return state with { Mode = InputMode.Normal, Jump = null, PendingKeys = string.Empty, PaletteText = string.Empty };
    }

    /// <summary>Puts <paramref name="key"/> first among the recently opened keys of the session.</summary>
    public static AppState Remember(AppState state, string key)
    {
        var recent = new List<string> { key };

        recent.AddRange(state.Session.RecentKeys.Where(known => known != key).Take(JumpRows.MaxRecent - 1));

        return state with { Session = state.Session with { RecentKeys = recent } };
    }

    private static ReduceResult OpenKey(AppState state, string key)
    {
        var effects = new List<Effect>();
        var shown = state;

        if (state.Session.Active is null)
        {
            var view = ViewReducer.Reduce(state, new ViewOpenRequested(KeyView(key)));

            shown = view.State;
            effects.AddRange(view.Effects);
        }

        var pushed = DetailReducer.Open(shown, shown.Session.Active!.Value, key, null, effects);

        return pushed == shown ? Plain(shown with { Message = StackFullMessage }) : new ReduceResult(pushed, effects);
    }

    private static ViewDefinition KeyView(string key)
    {
        var query = $"{JqlKeyField} = {key}";

        return new ViewDefinition(ViewSourceKind.Jql, query, ViewLayout.List, query);
    }

    private static string NoRowsNotice(AppState state, JumpState jump)
    {
        var parse = JumpKey.Parse(jump.Text, JumpRows.DefaultProject(state));

        return parse.Kind switch
        {
            JumpParseKind.Empty => EmptyNotice,
            JumpParseKind.NeedsProject => NeedsProjectNotice,
            _ => $"\"{jump.Text.Trim()}\" is not an issue key: use a project key, a hyphen and a number, like GCM-12.",
        };
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
