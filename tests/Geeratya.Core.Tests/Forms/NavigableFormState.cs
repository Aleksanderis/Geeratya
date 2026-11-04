using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// Puts the form of the first tab into the navigate state. Only the create form has that state in production (no
/// multi-field edit form exists any more, #762); these tests exercise the shared reducer machinery with a plain
/// multi-field form.
/// </summary>
internal static class NavigableFormState
{
    public static AppState Apply(AppState state)
    {
        var tab = state.Session.Tabs[0];

        var navigating = tab with { Form = tab.Form! with { Navigable = true, Navigating = true, Suggest = null } };

        return state with { Session = state.Session with { Tabs = [navigating, .. state.Session.Tabs.Skip(1)] } };
    }
}
