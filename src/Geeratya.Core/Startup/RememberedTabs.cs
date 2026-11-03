using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Startup;

/// <summary>
/// The tabs of one profile as the next start reopens them, in tab order, with the index of the active one. Pure; the
/// file layer is <see cref="RememberedTabsStore"/>.
/// </summary>
/// <param name="Items">The tabs.</param>
/// <param name="Active">The index of the active tab.</param>
public sealed record RememberedTabs(IReadOnlyList<RememberedTab> Items, int Active = 0)
{
    /// <summary>More tabs than this are dropped when a file is read.</summary>
    public const int MaxTabs = 50;

    /// <summary>A longer source value is dropped when a file is read.</summary>
    public const int MaxValueLength = 4000;

    public static RememberedTabs Empty { get; } = new([]);

    /// <summary>The tabs of the session; empty while there are none.</summary>
    public static RememberedTabs Of(SiteSession session)
    {
        var items = session.Tabs
            .Take(MaxTabs)
            .Select(tab => new RememberedTab(
                tab.Definition.Source.ToString(),
                tab.Definition.SourceValue,
                tab.Definition.Layout.ToString(),
                tab.Definition.Title,
                tab.ActiveQuickFilters.Count == 0 ? null : tab.ActiveQuickFilters,
                tab.Swimlane == SwimlaneKind.None ? null : tab.Swimlane.ToString(),
                tab.Swimlane == SwimlaneKind.None ? null : tab.Lane))
            .ToArray();

        var active = session.Tabs.ToList().FindIndex(tab => tab.Id == session.Active);

        return new RememberedTabs(items, Math.Max(0, active));
    }

    /// <summary>The same tabs with bad entries and oversize values removed (a file is untrusted input).</summary>
    public RememberedTabs Normalized()
    {
        var kept = new List<RememberedTab>();
        var active = -1;

        for (var index = 0; index < Items.Count && kept.Count < MaxTabs; index++)
        {
            if (ToRestored(Items[index]) is null)
            {
                continue;
            }

            if (index == Active)
            {
                active = kept.Count;
            }

            kept.Add(Items[index]);
        }

        return new RememberedTabs(kept, Math.Max(0, active));
    }

    /// <summary>The tabs to reopen and the index of the active one among them; unknown entries are skipped.</summary>
    public (IReadOnlyList<RestoredTab> Tabs, int Active) ToRestored()
    {
        var restored = new List<RestoredTab>();
        var active = 0;

        for (var index = 0; index < Items.Count; index++)
        {
            if (ToRestored(Items[index]) is not { } tab)
            {
                continue;
            }

            if (index == Active)
            {
                active = restored.Count;
            }

            restored.Add(tab);
        }

        return (restored, active);
    }

    /// <summary>Both hold the same tabs and the same active index.</summary>
    public bool SameAs(RememberedTabs other)
    {
        return Active == other.Active
            && Items.Count == other.Items.Count
            && Items.Zip(other.Items).All(pair => SameTab(pair.First, pair.Second));
    }

    private static bool SameTab(RememberedTab left, RememberedTab right)
    {
        return left.Source == right.Source
            && left.SourceValue == right.SourceValue
            && left.Layout == right.Layout
            && left.Title == right.Title
            && left.Swimlane == right.Swimlane
            && left.Lane == right.Lane
            && (left.QuickFilters ?? []).SequenceEqual(right.QuickFilters ?? []);
    }

    private static RestoredTab? ToRestored(RememberedTab? tab)
    {
        if (tab?.SourceValue is null || tab.Title is null || tab.SourceValue.Length > MaxValueLength
            || !TryParse<ViewSourceKind>(tab.Source, out var source) || !TryParse<ViewLayout>(tab.Layout, out var layout))
        {
            return null;
        }

        var swimlane = TryParse<SwimlaneKind>(tab.Swimlane, out var kind) ? kind : SwimlaneKind.None;

        return new RestoredTab(
            new ViewDefinition(source, tab.SourceValue, layout, tab.Title),
            tab.QuickFilters ?? [],
            swimlane,
            swimlane == SwimlaneKind.None ? null : tab.Lane);
    }

    private static bool TryParse<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        return text is not null && Enum.TryParse(text, out value) && Enum.IsDefined(value) && value.ToString() == text;
    }
}
