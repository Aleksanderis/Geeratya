using Geeratya.Core.Sources;

namespace Geeratya.Core.SavedViews;

/// <summary>The saved-views file could not be written; the status line says so.</summary>
public sealed record SavedViewsFailed(string Message) : SourceCommand;
