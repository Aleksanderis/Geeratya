using Geeratya.Core.Forms;
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
    public void GivenASingleChoiceThatIsNotRequired_WhenOpened_ThenNoneIsOfferedFirstAndClearsTheValue()
    {
        var field = Field(FormFieldKind.Select) with { Value = FormValue.OfChosen(["2"]) };

        var visible = FormEditor.VisibleOptions(field);
        var cleared = FormEditor.Accept(field);

        Assert.Equal(["(none)", "Alpha", "Beta", "Alphabet"], visible.Select(option => option.Label));
        Assert.Empty(cleared.Field.Value.Chosen);
        Assert.True(cleared.Advance);
    }

    [Fact]
    public void GivenATypedFilter_WhenChoosing_ThenOnlyMatchesShowAndTheCursorPicksAmongThem()
    {
        var typed = FormEditor.Type(Field(FormFieldKind.Select), "alph");

        var moved = FormEditor.Move(typed, 5);
        var chosen = FormEditor.Accept(moved);

        Assert.Equal(["Alpha", "Alphabet"], FormEditor.VisibleOptions(typed).Select(option => option.Label));
        Assert.Equal(["3"], chosen.Field.Value.Chosen);
        Assert.Equal(string.Empty, chosen.Field.Entry);
    }

    [Fact]
    public void GivenAMultiChoice_WhenEnterIsPressedTwiceOnTheSameOption_ThenItIsToggledOnAndOff()
    {
        var field = Field(FormFieldKind.MultiSelect);

        var on = FormEditor.Accept(field);
        var off = FormEditor.Accept(on.Field);

        Assert.Equal(["1"], on.Field.Value.Chosen);
        Assert.False(on.Advance);
        Assert.Empty(off.Field.Value.Chosen);
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
