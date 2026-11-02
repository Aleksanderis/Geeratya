using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class EditRequestTests
{
    [Fact]
    public void GivenALabelsEdit_WhenPlanned_ThenTheRowGetsTheLabelsAndTheUndoRestoresThemOnlyWhileUnchanged()
    {
        var request = Labels("a", "b");
        var change = request.Plan(Row("old"));

        var patched = change!.Patch(Row("old"));
        var reverted = change.Revert(patched);
        var newer = change.Revert(Row("reloaded"));

        Assert.Equal(["a", "b"], patched.Labels);
        Assert.Equal(["old"], reverted.Labels);
        Assert.Equal(["reloaded"], newer.Labels);
    }

    [Fact]
    public void GivenAnEditOfFieldsARowDoesNotShow_WhenPlanned_ThenThereIsNoRowChangeAndNoReadCanProveIt()
    {
        var change = new FieldChange("duedate", FormFieldKind.Date, FormValue.OfText("2026-11-30"));
        var request = new EditRequest("A-1", [change]);

        Assert.Null(request.Plan(Row("x")));
        Assert.False(request.IsShownBy(Row("x")));
    }

    [Fact]
    public void GivenALabelsEdit_WhenARowIsRead_ThenOnlyARowWithThoseLabelsShowsIt()
    {
        var request = Labels("a");

        Assert.True(request.IsShownBy(Row("a")));
        Assert.False(request.IsShownBy(Row("b")));
    }

    private static EditRequest Labels(params string[] labels)
    {
        return new EditRequest("A-1", [new FieldChange("labels", FormFieldKind.Labels, FormValue.OfChosen(labels))]);
    }

    private static IssueSummary Row(params string[] labels)
    {
        return new IssueSummary(
            "1",
            "A-1",
            "One",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "A");
    }
}
