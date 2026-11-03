using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs the saved-view effects (read, save, rename, delete in the profile's file) and answers each with the whole
/// stored list. A file that cannot be read or written is answered with <see cref="SavedViewsFailed"/>; the list on
/// screen keeps the change until the next reply replaces it.
/// </summary>
public sealed class SavedViewEffectHandler
{
    private readonly SavedViewStore _store;
    private readonly object _gate = new();
    private long _version;

    public SavedViewEffectHandler(SavedViewStore store)
    {
        _store = store;
    }

    /// <summary>The command that reports the stored views, or null when the effect is not a saved-view effect.</summary>
    public Command? Run(Effect effect)
    {
        if (effect is not (LoadSavedViewsEffect or SaveViewEffect or RenameSavedViewEffect or DeleteSavedViewEffect))
        {
            return null;
        }

        try
        {
            lock (_gate)
            {
                return effect switch
                {
                    LoadSavedViewsEffect load => Reply(load.Profile, _store.Load(load.Profile)),
                    SaveViewEffect save => Reply(save.Profile, _store.Update(save.Profile, views => views.Saved(save.View))),
                    RenameSavedViewEffect rename => Reply(
                        rename.Profile,
                        _store.Update(rename.Profile, views => views.Renamed(rename.OldName, rename.NewName))),
                    DeleteSavedViewEffect delete => Reply(
                        delete.Profile,
                        _store.Update(delete.Profile, views => views.Removed(delete.Name))),
                    _ => null,
                };
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SavedViewsFailed($"Could not write the saved views: {exception.Message}");
        }
    }

    private SavedViewsLoaded Reply(Profile profile, SavedViewList views)
    {
        return new SavedViewsLoaded(profile, views, ++_version);
    }
}
