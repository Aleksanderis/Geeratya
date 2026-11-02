using Geeratya.Core.State;

namespace Geeratya.Core.Sources;

/// <summary>
/// Load the boards and/or saved filters for the picker. The host answers with <see cref="BoardsLoaded"/>,
/// <see cref="FiltersLoaded"/> or <see cref="SourceListFailed"/>, one per requested list.
/// </summary>
public sealed record LoadSourcesEffect(bool Boards, bool Filters) : Effect;
