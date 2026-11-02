using Geeratya.Core.Forms;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class FormEditorTests
{
    private static readonly FieldOption[] _options = [new("1", "Alpha"), new("2", "Beta"), new("3", "Alphabet")];

    [Fact]
    public void GivenAWideAndACombiningCharacter_WhenBackspacing_ThenTheWholeCharacterGoes()
    {
        var field = FormEditor.Type(Field(FormFieldKind.Text), "a日é");

        var once = FormEditor.Backspace(field);
        var twice = FormEditor.Backspace(once);

        Assert.Equal("a日", once.Value.Text);
        Assert.Equal("a", twice.Value.Text);
    }

    [Fact]
    public void GivenAParagraph_WhenEnterIsPressed_ThenItAddsALineAndDoesNotAdvance()
    {
        var field = FormEditor.Type(Field(FormFieldKind.Paragraph), "one");

        var edit = FormEditor.Accept(field);

        Assert.Equal("one\n", edit.Field.Value.Text);
        Assert.False(edit.Advance);
    }

    [Fact]
    public void GivenALongText_WhenTyping_ThenItStopsAtJirasLimit()
    {
        var field = FormEditor.Type(Field(FormFieldKind.Text), new string('x', FormEditor.MaxTextLength + 50));

        Assert.Equal(FormEditor.MaxTextLength, field.Value.Text.Length);
    }

    [Fact]
    public void GivenASingleChoice_WhenChoosingNone_ThenTheValueIsClearedAndTheFilterEmptied()
    {
        var field = Field(FormFieldKind.Select) with { Value = FormValue.OfChosen(["2"]), Entry = "no" };

        var cleared = FormEditor.Choose(field, new Suggestion(string.Empty, "(none)", null, false));

        Assert.Empty(cleared.Value.Chosen);
        Assert.Equal(string.Empty, cleared.Entry);
    }

    [Fact]
    public void GivenASingleChoice_WhenChoosingAnOption_ThenItReplacesTheValue()
    {
        var field = Field(FormFieldKind.Select) with { Value = FormValue.OfChosen(["2"]) };

        var chosen = FormEditor.Choose(field, new Suggestion("3", "Alphabet", null, false));

        Assert.Equal(["3"], chosen.Value.Chosen);
    }

    [Fact]
    public void GivenAMultiChoice_WhenChoosingTwiceAndBackspacing_ThenChipsAreAddedOnceAndRemovedFromTheEnd()
    {
        var field = Field(FormFieldKind.MultiSelect);

        var one = FormEditor.Choose(field, new Suggestion("1", "Alpha", null, false));
        var two = FormEditor.Choose(one, new Suggestion("2", "Beta", null, false));
        var again = FormEditor.Choose(two, new Suggestion("2", "Beta", null, false));
        var dropped = FormEditor.Backspace(again);

        Assert.Equal(["1", "2"], again.Value.Chosen);
        Assert.Equal(["1"], dropped.Value.Chosen);
    }

    [Fact]
    public void GivenASingleChoiceWithAFilter_WhenBackspacing_ThenTheFilterShrinksBeforeTheValueGoes()
    {
        var field = FormEditor.Type(Field(FormFieldKind.Select) with { Value = FormValue.OfChosen(["2"]) }, "a");

        var filter = FormEditor.Backspace(field);
        var value = FormEditor.Backspace(filter);

        Assert.Equal(string.Empty, filter.Entry);
        Assert.Equal(["2"], filter.Value.Chosen);
        Assert.Empty(value.Value.Chosen);
    }

    [Fact]
    public void GivenAChoiceField_WhenEnterIsPressedWithoutAPopupRow_ThenItMovesOn()
    {
        Assert.True(FormEditor.Accept(Field(FormFieldKind.Select)).Advance);
        Assert.True(FormEditor.Accept(Field(FormFieldKind.MultiSelect)).Advance);
    }

    [Fact]
    public void GivenADuplicateLabel_WhenAdded_ThenItIsKeptOnce()
    {
        var field = Field(FormFieldKind.Labels).WithInitial(FormValue.OfChosen(["a"]));

        var typed = FormEditor.Type(field, "a b a ");

        Assert.Equal(["a", "b"], typed.Value.Chosen);
        Assert.Equal(string.Empty, typed.Entry);
    }

    [Fact]
    public void GivenAReadOnlyField_WhenKeysArrive_ThenNothingChangesAndEnterMovesOn()
    {
        var field = Field(FormFieldKind.Unsupported);

        Assert.Equal(field, FormEditor.Type(field, "x"));
        Assert.Equal(field, FormEditor.Backspace(field));
        Assert.True(FormEditor.Accept(field).Advance);
    }

    [Fact]
    public void GivenALabelBeingTyped_WhenTheFormIsChecked_ThenItCountsAsAChange()
    {
        var field = FormEditor.Type(Field(FormFieldKind.Labels), "x");

        Assert.True(field.IsChanged);
        Assert.Equal(["x"], FormEditor.Committed(field).Value.Chosen);
    }

    private static FormField Field(FormFieldKind kind)
    {
        return FormField.Create("f", "F", kind, false, _options);
    }
}
