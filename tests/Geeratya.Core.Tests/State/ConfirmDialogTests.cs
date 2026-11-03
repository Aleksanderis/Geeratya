using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class ConfirmDialogTests
{
    [Fact]
    public void GivenAQuestionWithAWarning_WhenSplitting_ThenTheUndoSentenceIsTheWarningAndWordsAreKept()
    {
        var split = ConfirmDialog.Split("Delete PROJ-1? Jira cannot undo this");

        Assert.Equal("Delete PROJ-1?", split.Question);
        Assert.Equal("Jira cannot undo this", split.Warning);
    }

    [Fact]
    public void GivenAQuotedCommentThatMentionsUndo_WhenSplitting_ThenTheQuoteStaysInTheQuestion()
    {
        const string message = "Delete the A-1 comment? Jira cannot undo this. By Eve: \"I cannot undo. Really? ok\"";

        var split = ConfirmDialog.Split(message);

        Assert.Equal("Delete the A-1 comment? By Eve: \"I cannot undo. Really? ok\"", split.Question);
        Assert.Equal("Jira cannot undo this.", split.Warning);
    }

    [Fact]
    public void GivenAQuestionWithoutAWarning_WhenSplitting_ThenTheWarningIsEmpty()
    {
        Assert.Equal(string.Empty, ConfirmDialog.Split("Quit anyway?").Warning);
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
        var request = new ConfirmRequest("Delete PROJ-1? Jira cannot undo this", "x");

        Assert.Null(ConfirmDialog.Layout(request, 80, 7));
        Assert.NotNull(ConfirmDialog.Layout(request, 80, 8));
        Assert.Null(ConfirmDialog.Layout(request, 27, 30));
        Assert.NotNull(ConfirmDialog.Layout(request, 28, 30));
    }
}
