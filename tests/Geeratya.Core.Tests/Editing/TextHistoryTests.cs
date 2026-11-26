using Geeratya.Core.Editing;

namespace Geeratya.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class TextHistoryTests
{
    [Fact]
    public void GivenSingleCharactersTyped_WhenUndone_ThenTheWordGoesAtOnce()
    {
        var typed = TextBuffer.Empty.Insert("a").Insert("b").Insert(" ").Insert("c");

        var undone = typed.Apply(EditorOperation.Undo);

        Assert.Equal("ab ", undone.Text);
        Assert.Equal("ab", undone.Apply(EditorOperation.Undo).Text);
        Assert.Equal(string.Empty, undone.Apply(EditorOperation.Undo).Apply(EditorOperation.Undo).Text);
    }

    [Fact]
    public void GivenAPaste_WhenUndone_ThenItIsOneStepWhateverItsSize()
    {
        var pasted = TextBuffer.Of("x").Insert("hello world");

        Assert.Equal("x", pasted.Apply(EditorOperation.Undo).Text);
    }

    [Fact]
    public void GivenAnUndo_WhenRedone_ThenTheCursorAndSelectionOfTheStepsComeBack()
    {
        var selected = TextBuffer.Of("hello").SelectAll();
        var replaced = selected.Insert("X");

        var undone = replaced.Apply(EditorOperation.Undo);

        Assert.True(undone.HasSelection);
        Assert.Equal("hello", undone.SelectedText);
        Assert.Equal(new TextBuffer("X", 1), undone.Apply(EditorOperation.Redo));
    }

    [Fact]
    public void GivenMoreThanTheLimit_WhenEditing_ThenTheOldestStepsGo()
    {
        var buffer = TextBuffer.Empty;

        for (var step = 0; step < TextHistory.MaxSteps + 20; step++)
        {
            buffer = buffer.Insert("ab").Insert("\n");
        }

        Assert.Equal(TextHistory.MaxSteps, buffer.History.Undo.Length);
    }

    [Fact]
    public void GivenATextSetOutsideTheEditingKeys_WhenUndone_ThenNothingOlderComesBack()
    {
        var typed = TextBuffer.Empty.Insert("a").Insert("\n").Insert("b");
        var foreign = typed with { Text = "replaced from outside", Cursor = 3 };

        Assert.Equal(foreign, foreign.Apply(EditorOperation.Undo));
        Assert.Equal(foreign, foreign.Apply(EditorOperation.Redo));
    }

    [Fact]
    public void GivenTheHistory_WhenComparingBuffers_ThenOnlyTextCursorAndSelectionCount()
    {
        var typed = TextBuffer.Empty.Insert("a");

        Assert.Equal(TextBuffer.Of("a"), typed);
        Assert.Equal(TextBuffer.Of("a").GetHashCode(), typed.GetHashCode());
    }

    [Fact]
    public void GivenAReplaceByASuggestion_WhenUndone_ThenTheTriggerTextIsBack()
    {
        var typed = TextBuffer.Rich("hi @ol");

        var replaced = typed.Replace(3, 6, "@[Olga](mention:1) ");

        Assert.Equal("hi @ol", replaced.Apply(EditorOperation.Undo).Text);
    }
}
