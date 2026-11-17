using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.Tests.State;

/// <summary>
/// The one discard prompt of every editor (<c>docs/internal/ui-consistency.md</c>, component 3): a field form and the
/// profile form ask through the same <see cref="ConfirmRequest"/>; <c>y</c> throws the text away, <c>n</c>, <c>q</c>
/// and <c>Esc</c> go back to the editor as it was, <c>Enter</c> never answers. The comment editor's is covered in
/// <c>CommentEditingTests</c>.
/// </summary>
[Trait("Os", "Independent")]
public sealed class DiscardPromptTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    public static TheoryData<string> Editors()
    {
        return ["form", "profile"];
    }

    [Theory]
    [MemberData(nameof(Editors))]
    public void GivenAChangedEditor_WhenEscIsPressedTwice_ThenItAsksThroughOneDiscardConfirm(string editor)
    {
        var asked = Asked(editor);

        Assert.Equal(InputMode.Confirm, asked.Mode);
        Assert.True(DiscardPrompts.Is(asked.Confirm));
        Assert.Contains("Discard", asked.Confirm!.Message);
    }

    [Theory]
    [MemberData(nameof(Editors))]
    public void GivenTheDiscardQuestion_WhenYIsPressed_ThenTheEditorClosesAndNothingIsSent(string editor)
    {
        var discarded = Press(Asked(editor), "y");

        Assert.False(IsOpen(discarded.State, editor));
        Assert.Null(discarded.State.Confirm);
        Assert.DoesNotContain(discarded.Effects, static effect => effect is ExecuteWriteEffect);
    }

    [Theory]
    [MemberData(nameof(Editors))]
    public void GivenTheDiscardQuestion_WhenNQOrEscIsPressed_ThenTheEditorStaysWithItsText(string editor)
    {
        foreach (var key in new[] { "n", "q", "Esc" })
        {
            var kept = Press(Asked(editor), key);

            Assert.True(IsOpen(kept.State, editor), key);
            Assert.Null(kept.State.Confirm);
            Assert.Equal(InputMode.Insert, kept.State.Mode);
        }
    }

    [Theory]
    [MemberData(nameof(Editors))]
    public void GivenTheDiscardQuestion_WhenEnterIsPressed_ThenItIsNeverAnswered(string editor)
    {
        var asked = Asked(editor);

        var pressed = Press(asked, "Enter");

        Assert.Equal(InputMode.Confirm, pressed.State.Mode);
        Assert.True(DiscardPrompts.Is(pressed.State.Confirm));
        Assert.True(IsOpen(pressed.State, editor));
    }

    private static bool IsOpen(AppState state, string editor)
    {
        return editor == "form" ? state.Session.Tabs[0].Form is not null : state.Form is not null;
    }

    private AppState Asked(string editor)
    {
        return editor == "form" ? AskedForm() : AskedProfile();
    }

    // A one-field form with a typed filter is dirty; Esc asks.
    private AppState AskedForm()
    {
        var tabbed = Tabbed();
        var tab = tabbed.Session.Tabs[0];
        var opened = FormState.Opened(new AssignFormTarget("A-1", null), 1, null);
        var field = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []) with
        {
            Value = FormValue.OfText("changed"),
            Initial = FormValue.OfText("original"),
        };
        var form = opened with { Fields = [field], IsLoading = false };
        var state = tabbed with
        {
            Session = tabbed.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };

        var asked = Press(state, "Esc").State;

        Assert.Equal(InputMode.Confirm, asked.Mode);

        return asked;
    }

    private AppState AskedProfile()
    {
        var palette = Tabbed() with { Mode = InputMode.Command, PaletteText = "profile add" };
        var opened = _reducer.Reduce(palette, new KeyPressed("Enter", _at)).State;
        var typed = Press(opened, "w", "o", "r").State;

        return Press(typed, "Esc").State;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key, _at));
        }

        return result;
    }

    private AppState Tabbed()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");

        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }
}
