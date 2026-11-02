using Geeratya.Core.Writes;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The status picker of one issue: a one-field form listing the transitions Jira offers now. The opener passes no
/// choices; the metadata handler reads them and answers with the target filled in (<see cref="With"/>). When
/// <paramref name="ColumnStatusIds"/> is set (a card moved towards a board column) only the transitions that end in
/// those statuses are listed, and when exactly one does the form never shows: the move is sent at once.
/// </summary>
public sealed record TransitionPickTarget(
    string Key,
    IReadOnlyList<string> ColumnStatusIds,
    string? ColumnName,
    IReadOnlyList<TransitionChoice> Choices) : FormTarget
{
    /// <summary>The id of the one field of the picker.</summary>
    public const string FieldId = "transition";

    private const string PickLabel = "Move to";

    public override string Title => ColumnName is null ? $"Change status of {Key}" : $"Move {Key} to {ColumnName}";

    public override string? IssueKey => Key;

    public override string EmptyText => $"No transition is available for {Key}";

    public override bool ClosesOnSend => true;

    /// <summary>True for a move towards a column.</summary>
    public bool IsColumnMove => ColumnStatusIds.Count > 0;

    /// <summary>The transitions that end in the column; all of them for a plain picker.</summary>
    public IReadOnlyList<TransitionChoice> Matches =>
        IsColumnMove ? [.. Choices.Where(IsInColumn)] : Choices;

    public override bool ShowsWhileLoading => !IsColumnMove;

    public override IReadOnlyList<FieldChange>? Automatic =>
        IsColumnMove && Matches.Count == 1 ? [Change(Matches[0])] : null;

    /// <summary>The target with the transitions Jira offers.</summary>
    public TransitionPickTarget With(IEnumerable<Transition> transitions)
    {
        return this with { Choices = [.. transitions.Select(TransitionChoice.Of)] };
    }

    /// <summary>The one field of the form: the matching transitions, or all of them when none matches.</summary>
    public IReadOnlyList<FormField> Fields()
    {
        if (Choices.Count == 0)
        {
            return [];
        }

        var shown = IsColumnMove && Matches.Count > 0 ? Matches : Choices;
        var label = IsColumnMove && Matches.Count == 0 ? $"No transition leads to {ColumnName}; move to" : PickLabel;
        var options = shown.Select(choice => new FieldOption(choice.Id, choice.Label)).ToArray();

        return [FormField.Create(FieldId, label, FormFieldKind.Select, true, options)];
    }

    public override FormTarget? Following(IReadOnlyList<FieldChange> changes)
    {
        var choice = Chosen(changes);

        return choice is { NeedsForm: true } ? new TransitionFormTarget(Key, choice) : null;
    }

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var choice = Chosen(changes) ?? throw new InvalidOperationException("No transition was picked.");

        return new TransitionRequest(Key, choice, [], choice.AsksFirst);
    }

    private bool IsInColumn(TransitionChoice choice)
    {
        return ColumnStatusIds.Contains(choice.Target.Id);
    }

    private static FieldChange Change(TransitionChoice choice)
    {
        return new FieldChange(FieldId, FormFieldKind.Select, FormValue.OfChosen([choice.Id]));
    }

    private TransitionChoice? Chosen(IReadOnlyList<FieldChange> changes)
    {
        var chosen = changes.FirstOrDefault(change => change.FieldId == FieldId)?.Value.Chosen;
        var id = chosen is { Count: > 0 } ? chosen[0] : null;

        return Choices.FirstOrDefault(choice => choice.Id == id);
    }
}
