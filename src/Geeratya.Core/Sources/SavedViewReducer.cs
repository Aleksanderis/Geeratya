using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.State;

namespace Geeratya.Core.Sources;

/// <summary>
/// The <c>:view</c> command: open the saved-view picker, save the active tab's view under a name, rename or delete
/// one. Pure; the host reads and writes the file (<see cref="SaveViewEffect"/> and its siblings) and answers with
/// <see cref="SavedViewsLoaded"/>. Saved views are local data: nothing is sent to Jira and no confirmation is needed,
/// since a view is a name for a source and layout that can be saved again.
/// </summary>
public static class SavedViewReducer
{
    private const string NoProfileMessage = "No profile yet: add one with :profile add.";
    private const string NoViewMessage = "No view to save: open a tab first.";
    private const string LoadingMessage = "Saved views are loading: try the command again.";
    private const string OpenWord = "open";
    private const string SaveWord = "save";
    private const string RenameWord = "rename";
    private const string DeleteWord = "delete";
    private const char DoubleQuote = '"';
    private const char SingleQuote = '\'';

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Whether a reply belongs to the active profile and is newer than the loaded copy.</summary>
    public static bool IsFresh(SavedViewsLoaded loaded, AppState state)
    {
        return loaded.Profile == state.Profiles.Active && loaded.Version > state.Session.Sources.SavedViewsVersion;
    }

    /// <summary>Runs <c>view</c> with the text after the word.</summary>
    public static ReduceResult Palette(AppState state, string argument)
    {
        if (state.Profiles.Active is not { } profile)
        {
            return Failed(state, NoProfileMessage);
        }

        var words = Words(argument);
        var verb = words.Count == 0 ? OpenWord : words[0].ToLowerInvariant();

        return verb switch
        {
            SaveWord => Save(state, profile, words),
            RenameWord => Rename(state, profile, words),
            DeleteWord => Delete(state, profile, words),
            OpenWord => SourceReducer.OpenScoped(state, PickerScope.Views, string.Join(' ', words.Skip(1))),
            _ => SourceReducer.OpenScoped(state, PickerScope.Views, string.Join(' ', words)),
        };
    }

    private static ReduceResult Save(AppState state, Profile profile, List<string> words)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (SavedViewList.CleanName(string.Join(' ', words.Skip(1))) is not { } name)
        {
            return Failed(state, $"Usage: {PaletteCommands.UsageOf(PaletteCommands.View)}");
        }

        if (tab is null)
        {
            return Failed(state, NoViewMessage);
        }

        var view = SavedViewList.FromDefinition(name, tab.Definition);
        var lists = state.Session.Sources;
        var known = lists.SavedViews;

        if (known is { Items.Count: >= SavedViewList.MaxViews } && known.Find(name) is null)
        {
            return Failed(state, $"Too many saved views ({SavedViewList.MaxViews}): delete one first.");
        }

        var replaced = known?.Find(name) is not null;
        var message = replaced ? $"Replaced saved view \"{name}\"." : $"Saved view \"{name}\".";
        var changed = known is null ? lists : lists with { SavedViews = known.Saved(view) };

        return Done(state, changed, message, new SaveViewEffect(profile, view));
    }

    private static ReduceResult Rename(AppState state, Profile profile, List<string> words)
    {
        var lists = state.Session.Sources;

        if (words.Count != 3 || SavedViewList.CleanName(words[2]) is not { } newName)
        {
            return Failed(state, $"Usage: {PaletteCommands.UsageOf(PaletteCommands.View)}");
        }

        if (lists.SavedViews is not { } known)
        {
            return Loading(state, profile);
        }

        if (known.Find(words[1]) is not { } existing)
        {
            return Failed(state, $"No saved view named \"{words[1]}\".");
        }

        if (known.Find(newName) is { } other
            && !string.Equals(other.Name, existing.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(state, $"A saved view named \"{other.Name}\" exists already.");
        }

        var changed = lists with { SavedViews = known.Renamed(existing.Name, newName) };

        return Done(
            state,
            changed,
            $"Renamed saved view \"{existing.Name}\" to \"{newName}\".",
            new RenameSavedViewEffect(profile, existing.Name, newName));
    }

    private static ReduceResult Delete(AppState state, Profile profile, List<string> words)
    {
        var lists = state.Session.Sources;

        if (words.Count != 2)
        {
            return Failed(state, $"Usage: {PaletteCommands.UsageOf(PaletteCommands.View)}");
        }

        if (lists.SavedViews is not { } known)
        {
            return Loading(state, profile);
        }

        if (known.Find(words[1]) is not { } existing)
        {
            return Failed(state, $"No saved view named \"{words[1]}\".");
        }

        var changed = lists with { SavedViews = known.Removed(existing.Name) };

        return Done(
            state,
            changed,
            $"Deleted saved view \"{existing.Name}\" (a local name only; the source in Jira is untouched).",
            new DeleteSavedViewEffect(profile, existing.Name));
    }

    private static ReduceResult Loading(AppState state, Profile profile)
    {
        var failed = state with { Message = LoadingMessage, MessageSeverity = StatusSeverity.Warning };

        return new ReduceResult(failed, [new LoadSavedViewsEffect(profile)]);
    }

    private static ReduceResult Done(AppState state, SourceLists lists, string message, Effect effect)
    {
        var next = state with { Session = state.Session with { Sources = lists }, Message = message };

        return new ReduceResult(next, [effect]);
    }

    private static ReduceResult Failed(AppState state, string message)
    {
        var failed = state with { Message = message, MessageSeverity = StatusSeverity.Warning };

        return new ReduceResult(failed, _noEffects);
    }

    /// <summary>Splits on blanks; a word in matching quotes keeps its blanks and loses the quotes.</summary>
    private static List<string> Words(string argument)
    {
        var words = new List<string>();
        var text = argument.Trim();
        var index = 0;

        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;

                continue;
            }

            var quote = text[index];
            var closing = quote is DoubleQuote or SingleQuote ? text.IndexOf(quote, index + 1) : -1;

            if (closing > 0)
            {
                words.Add(text[(index + 1)..closing]);
                index = closing + 1;

                continue;
            }

            var end = index;

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            words.Add(text[index..end]);
            index = end;
        }

        return words;
    }
}
