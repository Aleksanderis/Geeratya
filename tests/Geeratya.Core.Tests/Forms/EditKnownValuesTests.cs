using Geeratya.Core.Forms;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class EditKnownValuesTests
{
    private static readonly EditFormTarget _target = new(
        "X-1",
        new Dictionary<string, FormValue> { ["summary"] = FormValue.OfText("Title") },
        true,
        null);

    [Fact]
    public void GivenEditableFields_WhenAskingWhatIsMissing_ThenKnownReadOnlyAndPickerFieldsAreSkipped()
    {
        var fields = new[]
        {
            Field("summary", EditFieldKind.Text),
            Field("duedate", EditFieldKind.Date),
            Field("components", EditFieldKind.MultiOption),
            Field("assignee", EditFieldKind.User),
            Field("parent", EditFieldKind.Parent),
            Field("customfield_9", EditFieldKind.Unsupported),
            Field("customfield_8", EditFieldKind.Number, []),
        };

        var missing = EditKnownValues.Missing(fields, _target);

        Assert.Equal(["duedate", "components"], missing.Select(field => field.FieldId));
    }

    [Fact]
    public void GivenAFormThatShowsEveryField_WhenAskingWhatIsMissing_ThenNothingIsRead()
    {
        var missing = EditKnownValues.Missing([Field("duedate", EditFieldKind.Date)], _target with { OnlyKnown = false });

        Assert.Empty(missing);
    }

    [Fact]
    public void GivenAnIssueLimitedToOneField_WhenAskingWhatIsMissing_ThenOnlyThatFieldIsRead()
    {
        var fields = new[] { Field("duedate", EditFieldKind.Date), Field("components", EditFieldKind.MultiOption) };

        var missing = EditKnownValues.Missing(fields, _target with { OnlyId = "duedate" });

        Assert.Equal(["duedate"], missing.Select(field => field.FieldId));
    }

    [Fact]
    public void GivenReadValues_WhenMerged_ThenTheyAreKnownInTheShapeOfEachKind()
    {
        var moment = new DateTimeOffset(2026, 3, 1, 10, 15, 0, TimeSpan.Zero);
        var values = new Dictionary<string, IssueFieldValue>
        {
            ["duedate"] = IssueFieldValue.Empty with { Text = "2026-12-24" },
            ["components"] = IssueFieldValue.Empty with { Ids = ["1", "2"] },
            ["when"] = IssueFieldValue.Empty with { Moment = moment },
            ["blank"] = IssueFieldValue.Empty,
        };

        var merged = EditKnownValues.Merge(_target, values);

        Assert.Equal("2026-12-24", merged.Known["duedate"].Text);
        Assert.Equal(["1", "2"], merged.Known["components"].Chosen);
        Assert.Equal(moment.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), merged.Known["when"].Text);
        Assert.True(merged.Known["blank"].IsEmpty);
        Assert.Equal("Title", merged.Known["summary"].Text);
    }

    [Fact]
    public void GivenARichTextThatDoesNotSurviveTheText_WhenMerged_ThenItIsReadOnlyNotKnown()
    {
        const string tableJson = """
            {"type":"doc","version":1,"content":[{"type":"table","content":[{"type":"tableRow","content":[
            {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"a"}]}]}]}]}]}
            """;
        var table = AdfParser.Parse(tableJson);
        var values = new Dictionary<string, IssueFieldValue>
        {
            ["customfield_5"] = IssueFieldValue.Empty with { Document = table },
        };

        var merged = EditKnownValues.Merge(_target, values);

        Assert.False(merged.Known.ContainsKey("customfield_5"));
        Assert.Contains("customfield_5", merged.ReadOnlyIds!);
    }

    private static EditField Field(string id, EditFieldKind kind, string[]? operations = null)
    {
        return new EditField(id, id, false, "t", string.Empty, operations ?? ["set"], [], false, kind);
    }
}
