using Geeratya.Core.Boards;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The Details pane of the issue detail (<c>docs/ux-screens.md</c>, "Pane layout for the issue detail"): for each
/// field its label above a filled value block (<see cref="FieldBlock"/>), then muted rows for the dates and the
/// attachments. One cursor stop per field. A field shows the dropdown mark only when the edit metadata of the issue
/// allows it (<see cref="DetailState.EditableFields"/>); status is always marked, it moves by transition. Pure.
/// </summary>
internal static class DetailsPane
{
    private const string Title = "Details";
    private const string TransitionNote = "moves via transition";
    private const string UnassignedName = "Unassigned";
    private const string UnassignedChip = "[--]";
    private const string NoPriority = "None";
    private const string NoLabels = "none";
    private const string StatusDot = "●";
    private const string High = "▲";
    private const string Medium = "=";
    private const string Low = "▼";
    private const int InitialsCells = 2;

    public static void Add(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var header = HeaderFields.Of(detail);
        var content = new PanelContent();
        var first = true;

        foreach (var field in Fields(header, detail))
        {
            if (!first)
            {
                content.AddGap(StyledLine.Empty);
            }

            content.AddBlock(FieldBlock.Rows(field.Spec, inner), new DetailTarget(DetailTargetKind.Field, field.Id));
            first = false;
        }

        if (content.Lines.Count == 0)
        {
            return;
        }

        AddMuted(content, MutedRows(header, detail, inner));
        sink.AddPanel(PaneIds.Details, [new StyledSpan(Title, TextRole.Heading)], content, width);
    }

    /// <summary>Whether the Details pane lists the field as editable now (the mark is drawn, Enter opens it).</summary>
    public static bool IsEditable(DetailState detail, string fieldId)
    {
        return fieldId switch
        {
            DetailFieldIds.Status or DetailFieldIds.Sprint or DetailFieldIds.Estimate => true,
            DetailFieldIds.Priority or DetailFieldIds.Assignee or DetailFieldIds.Labels
                => detail.EditableFields?.Contains(fieldId) == true,
            _ => false,
        };
    }

    private static void AddMuted(PanelContent content, List<StyledLine> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        content.AddGap(StyledLine.Empty);

        foreach (var row in rows)
        {
            content.AddGap(row);
        }
    }

    private static List<StyledLine> MutedRows(HeaderFields header, DetailState detail, int inner)
    {
        var rows = new List<StyledLine>();

        if (header.Updated is { } updated && detail.LoadedAt is { } now)
        {
            rows.Add(Muted($"Updated {RelativeTime.Format(updated, now)}", inner));
        }

        if (detail.Issue is { Attachments.Count: > 0 } issue)
        {
            var count = issue.Attachments.Count;
            var name = TextClean.Of(issue.Attachments[0].FileName);

            rows.Add(Muted(count == 1 ? $"Attachments 1 · {name}" : $"Attachments {count} · {name}…", inner));
        }

        return rows;
    }

    private static StyledLine Muted(string text, int inner)
    {
        return StyledLine.Of(DisplayText.Cut(text, inner), TextRole.Muted);
    }

    private static List<Field> Fields(HeaderFields header, DetailState detail)
    {
        var fields = new List<Field>();

        if (header.Status is { } status)
        {
            var spec = FieldBlockSpec.OfValue("Status", StatusValue(status), true, TransitionNote);

            fields.Add(new Field(DetailFieldIds.Status, spec));
        }

        if (header.Known)
        {
            fields.Add(Of(detail, DetailFieldIds.Priority, "Priority", PriorityValue(header.Priority)));
            fields.Add(Of(detail, DetailFieldIds.Assignee, "Assignee", PersonValue(header.Assignee)));
        }

        if (header.Reporter is { } reporter)
        {
            fields.Add(Of(detail, DetailFieldIds.Reporter, "Reporter", PersonValue(reporter)));
        }

        if (header.Known)
        {
            fields.Add(Of(detail, DetailFieldIds.Labels, "Labels", LabelItems(header.Labels)));
        }

        AddText(fields, detail, DetailFieldIds.Parent, "Parent", header.Parent);
        AddText(fields, detail, DetailFieldIds.Epic, "Epic", header.Epic);
        AddText(fields, detail, DetailFieldIds.Sprint, "Sprint", header.Sprint);

        var estimate = string.IsNullOrWhiteSpace(detail.EstimateLabel)
            ? "Estimate"
            : TextClean.Of(detail.EstimateLabel);

        AddText(fields, detail, DetailFieldIds.Estimate, estimate, header.Estimate);

        return fields;
    }

    private static Field Of(
        DetailState detail,
        string id,
        string label,
        IReadOnlyList<IReadOnlyList<StyledSpan>> items)
    {
        return new Field(id, new FieldBlockSpec(label, items, IsEditable(detail, id)));
    }

    private static void AddText(List<Field> fields, DetailState detail, string id, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var text = new StyledSpan(TextClean.Of(value), TextRole.ValueBlock);

        fields.Add(new Field(id, FieldBlockSpec.OfValue(label, [text], IsEditable(detail, id))));
    }

    private static List<StyledSpan> StatusValue(IssueStatus status)
    {
        return
        [
            new StyledSpan(StatusDot, StatusRoles.Of(status)),
            new StyledSpan($" {TextClean.Of(status.Name)}", TextRole.ValueBlock),
        ];
    }

    private static IReadOnlyList<IReadOnlyList<StyledSpan>> PriorityValue(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return [[new StyledSpan(NoPriority, TextRole.ValueBlock)]];
        }

        var tier = PriorityTierOf.Name(priority);
        var glyph = tier switch
        {
            PriorityTier.High => High,
            PriorityTier.Medium => Medium,
            PriorityTier.Low => Low,
            _ => string.Empty,
        };
        var role = tier switch
        {
            PriorityTier.High => TextRole.Warning,
            PriorityTier.Low => TextRole.Muted,
            _ => TextRole.ValueBlock,
        };
        var name = TextClean.Of(priority);

        if (glyph.Length == 0)
        {
            return [[new StyledSpan(name, TextRole.ValueBlock)]];
        }

        return [[new StyledSpan(glyph, role), new StyledSpan($" {name}", TextRole.ValueBlock)]];
    }

    private static IReadOnlyList<IReadOnlyList<StyledSpan>> PersonValue(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return
            [
                [
                    new StyledSpan(UnassignedChip, TextRole.UnassignedChip),
                    new StyledSpan($" {UnassignedName}", TextRole.Muted),
                ],
            ];
        }

        var initials = AssigneeInitials.Of(name);
        var chip = $"[{initials.PadRight(InitialsCells)}]";

        var text = new StyledSpan($" {TextClean.Of(name)}", TextRole.ValueBlock);

        return [[new StyledSpan(chip, TextRole.InitialsChip), text]];
    }

    private static List<IReadOnlyList<StyledSpan>> LabelItems(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return [[new StyledSpan(NoLabels, TextRole.Muted)]];
        }

        var items = new List<IReadOnlyList<StyledSpan>>();

        foreach (var label in labels)
        {
            items.Add([new StyledSpan($"[{TextClean.Of(label)}]", LabelChips.For(label))]);
        }

        return items;
    }

    private sealed record Field(string Id, FieldBlockSpec Spec);
}
