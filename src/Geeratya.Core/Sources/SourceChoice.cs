using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sources;

/// <summary>One selectable picker row and the view it opens.</summary>
/// <param name="Kind">What the row opens.</param>
/// <param name="Name">The board or filter name, or the JQL of the query row.</param>
/// <param name="Detail">A short qualifier shown after the name (board kind and project, filter owner), may be empty.</param>
/// <param name="Definition">The view opened by choosing the row.</param>
public sealed record SourceChoice(SourceChoiceKind Kind, string Name, string Detail, ViewDefinition Definition)
{
    /// <summary>The sprint a <see cref="SourceChoiceKind.Sprint"/> row stands for.</summary>
    public Sprint? Sprint { get; init; }
}
