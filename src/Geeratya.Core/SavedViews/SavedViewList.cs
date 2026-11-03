using Geeratya.Core.Views;

namespace Geeratya.Core.SavedViews;

/// <summary>
/// The saved views of one profile, in the order they were saved. Pure: every change returns a new value. Names are
/// unique ignoring case.
/// </summary>
/// <param name="Items">The saved views.</param>
public sealed record SavedViewList(IReadOnlyList<SavedView> Items)
{
    /// <summary>The most views kept; saving one more is refused.</summary>
    public const int MaxViews = 200;

    /// <summary>A longer name is refused.</summary>
    public const int MaxNameLength = 100;

    /// <summary>A longer source value (a JQL text) is dropped when a file is read.</summary>
    public const int MaxValueLength = 4000;

    public static SavedViewList Empty { get; } = new([]);

    /// <summary>The name without surrounding blanks, or null when it is empty or too long.</summary>
    public static string? CleanName(string? name)
    {
        var text = (name ?? string.Empty).Trim();

        return text.Length is > 0 and <= MaxNameLength ? text : null;
    }

    /// <summary>The definition a view opens (its name is the tab title); null when the source or layout is unknown.</summary>
    public static ViewDefinition? ToDefinition(SavedView view)
    {
        if (!Enum.TryParse<ViewSourceKind>(view.Source, out var source) || !Enum.IsDefined(source)
            || !Enum.TryParse<ViewLayout>(view.Layout, out var layout) || !Enum.IsDefined(layout))
        {
            return null;
        }

        return new ViewDefinition(source, view.SourceValue, layout, view.Name);
    }

    /// <summary>The saved form of a definition under <paramref name="name"/>.</summary>
    public static SavedView FromDefinition(string name, ViewDefinition definition)
    {
        return new SavedView(name, definition.Source.ToString(), definition.SourceValue, definition.Layout.ToString());
    }

    public SavedView? Find(string name)
    {
        return Items.FirstOrDefault(item => Same(item.Name, name));
    }

    /// <summary>Adds the view, or replaces the one of the same name (keeping its place); the cap refuses a new one.</summary>
    public SavedViewList Saved(SavedView view)
    {
        var index = IndexOf(view.Name);

        if (index >= 0)
        {
            var items = Items.ToArray();

            items[index] = view;

            return new SavedViewList(items);
        }

        return Items.Count >= MaxViews ? this : new SavedViewList([.. Items, view]);
    }

    /// <summary>
    /// Renames a view; unchanged when <paramref name="oldName"/> is unknown or <paramref name="newName"/> is another
    /// view's name (a change of case only is allowed).
    /// </summary>
    public SavedViewList Renamed(string oldName, string newName)
    {
        var index = IndexOf(oldName);
        var clash = IndexOf(newName);

        if (index < 0 || (clash >= 0 && clash != index))
        {
            return this;
        }

        var items = Items.ToArray();

        items[index] = items[index] with { Name = newName };

        return new SavedViewList(items);
    }

    public SavedViewList Removed(string name)
    {
        return new SavedViewList([.. Items.Where(item => !Same(item.Name, name))]);
    }

    /// <summary>A copy that obeys the rules above, for data read from a file someone may have edited.</summary>
    public SavedViewList Normalized()
    {
        var result = Empty;

        // The serializer ignores nullability: a hand-edited file can hold nulls.
        // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        foreach (var item in Items ?? [])
        {
            if (item is null || CleanName(item.Name) is not { } name || item.SourceValue is null
                || item.SourceValue.Length > MaxValueLength || ToDefinition(item) is null)
            {
                continue;
            }

            result = result.Saved(item with { Name = name });
        }

        // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

        return result;
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private int IndexOf(string name)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Same(Items[index].Name, name))
            {
                return index;
            }
        }

        return -1;
    }
}
