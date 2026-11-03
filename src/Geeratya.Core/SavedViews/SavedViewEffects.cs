using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Core.SavedViews;

/// <summary>Read the profile's saved views from disk; answered with <see cref="SavedViewsLoaded"/>.</summary>
public sealed record LoadSavedViewsEffect(Profile Profile) : Effect;

/// <summary>Add or replace a saved view; answered with <see cref="SavedViewsLoaded"/>.</summary>
public sealed record SaveViewEffect(Profile Profile, SavedView View) : Effect;

/// <summary>Rename a saved view; answered with <see cref="SavedViewsLoaded"/>.</summary>
public sealed record RenameSavedViewEffect(Profile Profile, string OldName, string NewName) : Effect;

/// <summary>Delete a saved view; answered with <see cref="SavedViewsLoaded"/>.</summary>
public sealed record DeleteSavedViewEffect(Profile Profile, string Name) : Effect;
