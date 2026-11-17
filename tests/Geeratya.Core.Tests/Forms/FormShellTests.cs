using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The rules every form shares, whatever shows it (<see cref="FormShell"/>).</summary>
[Trait("Os", "Independent")]
public class FormShellTests
{
    [Fact]
    public void GivenMoreThanOneEditableField_WhenAsked_ThenTheFormIsNavigable()
    {
        FormField[] fields = [Field("a", FormFieldKind.Text), Field("b", FormFieldKind.Number)];

        Assert.True(FormShell.IsNavigable(fields));
    }

    [Fact]
    public void GivenOneEditableFieldAndReadOnlyOnes_WhenAsked_ThenTheFormIsNotNavigable()
    {
        FormField[] fields = [Field("a", FormFieldKind.Text), Field("b", FormFieldKind.Unsupported)];

        Assert.False(FormShell.IsNavigable(fields));
    }

    [Fact]
    public void GivenAFormWithNoFields_WhenAsked_ThenItIsNotNavigable()
    {
        Assert.False(FormShell.IsNavigable([]));
    }

    [Fact]
    public void GivenOneEditableField_WhenAsked_ThenTheFormHasOneField()
    {
        var form = Form(Field("a", FormFieldKind.Select), Field("b", FormFieldKind.Unsupported));

        Assert.True(FormShell.HasOneField(form));
        Assert.False(FormShell.HasOneField(Form(Field("a", FormFieldKind.Text), Field("b", FormFieldKind.Text))));
    }

    [Fact]
    public void GivenANavigableForm_WhenTheStateNoteIsAsked_ThenItNamesNavigateOrEditing()
    {
        var navigable = Form(Field("a", FormFieldKind.Text), Field("b", FormFieldKind.Text)) with { Navigable = true };

        Assert.Equal(FormShell.NavigateNote, FormShell.StateNote(navigable with { Navigating = true }));
        Assert.Equal(FormShell.EditingNote, FormShell.StateNote(navigable));
        Assert.Null(FormShell.StateNote(navigable with { Navigable = false }));
    }

    [Fact]
    public void GivenAFormOfAnIssue_WhenTheDiscardIsAsked_ThenItIsOneConfirmThatNamesTheIssueAndSaysNothingWasSaved()
    {
        var form = Form(Field("a", FormFieldKind.Text));

        var request = FormShell.DiscardRequest(form, new TabId(3));

        Assert.True(DiscardPrompts.Is(request));
        Assert.Contains("A-1", request.Message);
        Assert.Contains("Nothing has been saved", request.Message);
        Assert.Equal(new DiscardFormRequested(new TabId(3)), request.OnConfirm);
    }

    [Fact]
    public void GivenTheNewIssueForm_WhenTheDiscardIsAsked_ThenItSaysTheNewIssueIsLost()
    {
        var target = new CreateFormTarget("A", null, false, null);
        var form = FormState.Opened(target, 1, null);

        var request = FormShell.DiscardRequest(form, new TabId(1));

        Assert.Contains("the new issue", request.Message);
    }

    private static FormState Form(params FormField[] fields)
    {
        return FormState.Opened(new AssignFormTarget("A-1", null), 1, null) with { Fields = fields, IsLoading = false };
    }

    private static FormField Field(string id, FormFieldKind kind)
    {
        return FormField.Create(id, id, kind, false, []);
    }
}
