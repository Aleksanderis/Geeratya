using Geeratya.Core.Boards;
using Geeratya.Core.Views;

namespace Geeratya.Core.Startup;

/// <summary>A remembered tab turned back into what the view reducer opens.</summary>
public sealed record RestoredTab(
    ViewDefinition Definition,
    IReadOnlyList<long> QuickFilters,
    SwimlaneKind Swimlane,
    string? Lane,
    string? Name = null,
    int? DoneCutoff = null);
