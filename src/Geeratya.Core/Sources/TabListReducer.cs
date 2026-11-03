using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Sources;

/// <summary>
/// The tab list: the source picker narrowed to the open tabs (<see cref="PickerScope.Tabs"/>). It opens from the
/// <c>+n ›</c> overflow chip or <c>&lt;space&gt;T</c>; choosing a row switches to that tab. No network, no write.
/// </summary>
public static class TabListReducer
{
    private const string NoTabsMessage = "No tabs open.";
    private const string NoMatchNotice = "No tab matches.";

    /// <summary>Opens the list with the active tab highlighted; says so when no tab is open.</summary>
    public static ReduceResult Open(AppState state)
    {
        var tabs = state.Session.Tabs;

        if (tabs.Count == 0)
        {
            return new ReduceResult(state with { Message = NoTabsMessage }, []);
        }

        var choices = new List<SourceChoice>(tabs.Count);
        var selected = 0;

        for (var index = 0; index < tabs.Count; index++)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);
            var name = $"{number} {TabTitles.Label(tabs[index].Definition)}";

            choices.Add(new SourceChoice(SourceChoiceKind.Tab, name, string.Empty, tabs[index].Definition)
            {
                Tab = tabs[index].Id,
            });
            selected = tabs[index].Id == state.Session.Active ? index : selected;
        }

        var picker = new SourcePickerState(string.Empty, selected, PickerScope.Tabs, false, null) { Tabs = choices };
        var opened = SourceReducer.Close(state) with { Mode = InputMode.Insert, Picker = picker, Message = null };

        return new ReduceResult(opened, []);
    }

    /// <summary>The rows of the list narrowed by the typed text (case-insensitive substring of the row).</summary>
    public static IReadOnlyList<SourceChoice> Choices(SourcePickerState picker)
    {
        var text = picker.Text.Trim();
        var all = picker.Tabs ?? [];

        return text.Length == 0
            ? all
            : all.Where(choice => choice.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Closes the list and switches to the highlighted tab; an empty list only says so.</summary>
    public static ReduceResult Accept(AppState state, SourcePickerState picker)
    {
        var choices = Choices(picker);

        if (choices.Count == 0)
        {
            return new ReduceResult(state with { Picker = picker with { Notice = NoMatchNotice } }, []);
        }

        var choice = choices[Math.Clamp(picker.Selected, 0, choices.Count - 1)];

        return choice.Tab is { } tab
            ? ViewReducer.Reduce(SourceReducer.Close(state), new TabActivated(tab))
            : new ReduceResult(SourceReducer.Close(state), []);
    }
}
