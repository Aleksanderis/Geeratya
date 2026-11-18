using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class ConfirmDialogTests
{
    [Fact]
    public void GivenADestructiveRequest_WhenLayingOut_ThenTheDialogAddsTheOneWarningSentence()
    {
        var request = new ConfirmRequest("Delete PROJ-1?", "x", Destructive: true);

        var layout = ConfirmDialog.Layout(request, 80, 30)!;

        Assert.Equal(["Delete PROJ-1?"], layout.Question);
        Assert.Equal([ConfirmWording.CannotUndo], layout.Warning);
    }

    [Fact]
    public void GivenAQuotedCommentThatMentionsUndo_WhenLayingOut_ThenTheQuoteStaysInTheQuestion()
    {
        const string message = "Delete the A-1 comment? By Eve: \"I cannot undo. Really? ok\"";

        var layout = ConfirmDialog.Layout(new ConfirmRequest(message, "x", Destructive: true), 120, 30)!;

        Assert.Equal(message, string.Join(' ', layout.Question));
        Assert.Equal([ConfirmWording.CannotUndo], layout.Warning);
    }

    [Fact]
    public void GivenARequestThatIsNotDestructive_WhenLayingOut_ThenThereIsNoWarningEvenIfTheWordsMentionUndo()
    {
        var request = new ConfirmRequest("Quit anyway? Nothing to undo.", "x");

        Assert.Empty(ConfirmDialog.Layout(request, 80, 30)!.Warning);
    }

    [Fact]
    public void GivenADestructiveRequest_WhenSpoken_ThenTheWarningFollowsTheFirstSentenceBeforeTheQuote()
    {
        var request = new ConfirmRequest("Delete A-1? \"Fix login\"", "x", Destructive: true);

        Assert.Equal("Delete A-1? Jira cannot undo this. \"Fix login\"", ConfirmDialog.Spoken(request));
        Assert.Equal("Quit?", ConfirmDialog.Spoken(new ConfirmRequest("Quit?", "x")));
    }

    [Fact]
    public void GivenARequestWithItsOwnWarning_WhenLayingOut_ThenThatWarningIsUsed()
    {
        var request = new ConfirmRequest("Remove profile work?", "x", Destructive: true, Warning: "This cannot be undone.");

        Assert.Equal(["This cannot be undone."], ConfirmDialog.Layout(request, 80, 30)!.Warning);
    }

    [Fact]
    public void GivenWideAndLongWords_WhenWrapping_ThenNoLineIsWiderThanTheBoxAndNoCharacterIsLost()
    {
        const string text = "ab 日本語日本語日本語 abcdefghijklmnopqrstuvwxyz end";

        var lines = ConfirmDialog.Wrap(text, 10);

        Assert.All(lines, line => Assert.True(DisplayWidth.Of(line) <= 10, line));
        Assert.Equal(text.Replace(" ", string.Empty), string.Concat(lines).Replace(" ", string.Empty));
    }

    [Fact]
    public void GivenTheRequest_WhenLayingOutAtTheEdgeOfFitting_ThenItFitsExactlyAtItsHeight()
    {
        var request = new ConfirmRequest("Delete PROJ-1?", "x", Destructive: true);

        Assert.Null(ConfirmDialog.Layout(request, 80, 7));
        Assert.NotNull(ConfirmDialog.Layout(request, 80, 8));
        Assert.Null(ConfirmDialog.Layout(request, 27, 30));
        Assert.NotNull(ConfirmDialog.Layout(request, 28, 30));
    }
}
