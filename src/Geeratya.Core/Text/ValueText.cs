using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Text;

/// <summary>
/// The one place that turns a value (person, status, priority, issue type, estimate, date range, empty) into styled
/// spans or text, so every surface (list, card, backlog, Details, relation table, forms) draws the same value the same
/// way: the same chip, glyph, role and empty word (<see cref="EmptyValues"/>). A surface picks the variant that fits
/// its width (full, compact chip, glyph only); it never builds the spans itself. Pure.
/// </summary>
public static class ValueText
{
    /// <summary>The initials of an empty person, inside the brackets of the empty chip (<c>[--]</c>).</summary>
    public const string UnassignedInitials = "--";

    /// <summary>The status dot of the Details pane and the forms (the ASCII set swaps it later).</summary>
    public const string StatusDot = "●";

    private const string High = "▲";
    private const string Medium = "=";
    private const string Low = "▼";
    private const string DateFormat = "d MMM";
    private const string DateRangeSeparator = " - ";

    /// <summary>Whether <paramref name="name"/> reads as a person: not blank and with initials to show.</summary>
    public static bool IsPerson(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && AssigneeInitials.Of(name).Length > 0;
    }

    /// <summary>The initials of a person, or <see cref="UnassignedInitials"/> for none.</summary>
    public static string Initials(string? name)
    {
        return IsPerson(name) ? AssigneeInitials.Of(name!) : UnassignedInitials;
    }

    /// <summary>
    /// The compact variant of a person: the chip alone, <c> AB </c> in the person's colour or <c>[--]</c> muted, with
    /// the initials padded to <paramref name="cells"/> cells (cut when longer).
    /// </summary>
    public static StyledSpan PersonChip(string? name, int cells)
    {
        var initials = Initials(name);

        if (DisplayWidth.Of(initials) > cells)
        {
            initials = DisplayText.Cut(initials, cells);
        }

        if (IsPerson(name))
        {
            return new StyledSpan(PersonChips.Text(initials, cells), PersonChips.For(name!));
        }

        var pad = new string(' ', Math.Max(0, cells - DisplayWidth.Of(initials)));

        return new StyledSpan($"[{initials}{pad}]", TextRole.UnassignedChip);
    }

    /// <summary>The name of a person as text, or <see cref="EmptyValues.Unassigned"/>.</summary>
    public static string PersonName(string? name)
    {
        return IsPerson(name) ? TextClean.Of(name!) : EmptyValues.Unassigned;
    }

    /// <summary>The role of <see cref="PersonName"/>: <paramref name="filled"/> for a person, muted for the empty word.</summary>
    public static TextRole PersonNameRole(string? name, TextRole filled)
    {
        return IsPerson(name) ? filled : TextRole.Muted;
    }

    /// <summary>The full variant of a person: chip, a space, then the name (or the empty word, muted).</summary>
    public static IReadOnlyList<StyledSpan> Person(string? name, int cells, TextRole filled)
    {
        return
        [
            PersonChip(name, cells),
            new StyledSpan($" {PersonName(name)}", PersonNameRole(name, filled)),
        ];
    }

    /// <summary>The role of a status, by its category.</summary>
    public static TextRole StatusRole(IssueStatus status)
    {
        return StatusRoles.Of(status);
    }

    /// <summary>
    /// The dot and name of a status, both in the role of its category (the list, backlog, card and relation rows draw
    /// the name in the same role, so a status never reads plain in one place and coloured in another).
    /// </summary>
    public static IReadOnlyList<StyledSpan> Status(IssueStatus status)
    {
        var role = StatusRoles.Of(status);

        return
        [
            new StyledSpan(StatusDot, role),
            new StyledSpan($" {TextClean.Of(status.Name)}", role),
        ];
    }

    /// <summary>The glyph of a priority tier (<c>▲ = ▼</c>, the ASCII set swaps them later); empty for an unknown tier.</summary>
    public static string PriorityGlyph(string? priority)
    {
        return PriorityTierOf.Name(priority) switch
        {
            PriorityTier.High => High,
            PriorityTier.Medium => Medium,
            PriorityTier.Low => Low,
            _ => string.Empty,
        };
    }

    /// <summary>The colour role of a priority: red high, yellow medium, blue low (also for an unknown tier).</summary>
    public static TextRole PriorityRole(string? priority)
    {
        return PriorityTierOf.Name(priority) switch
        {
            PriorityTier.High => TextRole.PriorityHigh,
            PriorityTier.Medium => TextRole.PriorityMedium,
            _ => TextRole.PriorityLow,
        };
    }

    /// <summary>The full variant of a priority: glyph and name, the name alone for an unknown tier, the empty word for none.</summary>
    public static IReadOnlyList<StyledSpan> Priority(string? priority, TextRole name)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return [new StyledSpan(EmptyValues.None, TextRole.Muted)];
        }

        var text = TextClean.Of(priority);
        var glyph = PriorityGlyph(priority);

        return glyph.Length == 0
            ? [new StyledSpan(text, name)]
            : [new StyledSpan(glyph, PriorityRole(priority)), new StyledSpan($" {text}", name)];
    }

    /// <summary>An estimate: whole numbers without a decimal part, else two decimals at most.</summary>
    public static string Estimate(double estimate)
    {
        return EstimateText.Format(estimate);
    }

    /// <summary>An estimate, or an empty string when there is none.</summary>
    public static string Estimate(double? estimate)
    {
        return estimate is { } value ? Estimate(value) : string.Empty;
    }

    /// <summary>A day as <c>3 Nov</c>, in the invariant culture, by its UTC calendar day.</summary>
    public static string Day(DateTimeOffset date)
    {
        return date.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>A date range as <c>3 Nov - 17 Nov</c>; empty when either end is missing.</summary>
    public static string DateRange(DateTimeOffset? start, DateTimeOffset? end)
    {
        return start is { } from && end is { } to ? $"{Day(from)}{DateRangeSeparator}{Day(to)}" : string.Empty;
    }

    /// <summary>A due date as ISO text, or the empty word.</summary>
    public static string Due(DateOnly? due)
    {
        return due is { } date ? date.ToString("O", CultureInfo.InvariantCulture) : EmptyValues.None;
    }

    /// <summary>The empty word of a kind of value, muted: people read "Unassigned", everything else "None".</summary>
    public static StyledSpan Empty(bool person = false)
    {
        return new StyledSpan(person ? EmptyValues.Unassigned : EmptyValues.None, TextRole.Muted);
    }
}
