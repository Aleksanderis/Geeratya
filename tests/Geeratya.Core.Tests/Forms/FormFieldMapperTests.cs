using Geeratya.Core.Forms;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// Synthetic metadata: a custom field of every supported schema type and one the app has never seen. Nothing in the
/// form is listed in code, so each must map by its schema and the unknown one must stay in the list, read-only.
/// </summary>
[Trait("Os", "Independent")]
public class FormFieldMapperTests
{
    private static readonly FieldOption[] _choices = [new("1", "One"), new("2", "Two")];
    private static readonly EditFormTarget _all = new("X-1", new Dictionary<string, FormValue>(), false, null);

    [Fact]
    public void GivenCustomEditFieldsOfEverySchemaType_WhenMapped_ThenEachGetsTheKindOfItsSchema()
    {
        var fields = new[]
        {
            Edit("customfield_1", EditFieldKind.Text),
            Edit("customfield_2", EditFieldKind.RichText),
            Edit("customfield_3", EditFieldKind.Number),
            Edit("customfield_4", EditFieldKind.Date),
            Edit("customfield_5", EditFieldKind.DateTime),
            Edit("customfield_6", EditFieldKind.Option),
            Edit("customfield_7", EditFieldKind.MultiOption),
            Edit("customfield_8", EditFieldKind.Labels),
            Edit("customfield_9", EditFieldKind.User),
            Edit("customfield_10", EditFieldKind.Parent),
        };

        var mapped = FormFieldMapper.FromEdit(fields, _all);

        Assert.Equal(
            [
                FormFieldKind.Text,
                FormFieldKind.Paragraph,
                FormFieldKind.Number,
                FormFieldKind.Date,
                FormFieldKind.DateTime,
                FormFieldKind.Select,
                FormFieldKind.MultiSelect,
                FormFieldKind.Labels,
                FormFieldKind.User,
                FormFieldKind.Parent,
            ],
            mapped.Select(field => field.Kind));
    }

    [Fact]
    public void GivenAnUnknownSchemaTypeAndAFieldWithoutOperations_WhenMapped_ThenBothStayListedReadOnlyAtTheEnd()
    {
        var fields = new[]
        {
            Edit("customfield_90001", EditFieldKind.Unsupported),
            Edit("customfield_90005", EditFieldKind.Text, operations: []),
            Edit("summary", EditFieldKind.Text, required: true),
        };

        var mapped = FormFieldMapper.FromEdit(fields, _all);

        Assert.Equal(["summary", "customfield_90001", "customfield_90005"], mapped.Select(field => field.Id));
        Assert.All(mapped.Skip(1), field => Assert.False(field.IsEditable));
        Assert.All(mapped.Skip(1), field => Assert.Equal(FormFieldKind.Unsupported, field.Kind));
    }

    [Fact]
    public void GivenTheLabelsPilotTarget_WhenMapped_ThenOnlyTheLabelsFieldWithItsKnownValueRemains()
    {
        var known = new Dictionary<string, FormValue> { ["labels"] = FormValue.OfChosen(["a"]) };
        var target = new EditFormTarget("X-1", known, true, FormFieldKind.Labels);
        var fields = new[] { Edit("summary", EditFieldKind.Text), Edit("labels", EditFieldKind.Labels) };

        var mapped = FormFieldMapper.FromEdit(fields, target);

        var field = Assert.Single(mapped);
        Assert.Equal(["a"], field.Initial.Chosen);
        Assert.Equal(field.Initial, field.Value);
    }

    [Fact]
    public void GivenCreateFields_WhenMapped_ThenProjectAndIssueTypeAreLeftOutAndOptionsCarryOver()
    {
        var fields = new[]
        {
            new CreateField("project", "Project", true, false, CreateFieldKind.Fixed, [], null),
            new CreateField(
                "priority",
                "Priority",
                false,
                true,
                CreateFieldKind.SingleChoice,
                [new CreateAllowedValue("3", "High")],
                null),
            new CreateField("customfield_5", "Mystery", false, false, CreateFieldKind.Unsupported, [], null),
        };

        var mapped = FormFieldMapper.FromCreate(fields);

        Assert.Equal(["priority", "customfield_5"], mapped.Select(field => field.Id));
        Assert.Equal("High", mapped[0].Options[0].Label);
        Assert.False(mapped[1].IsEditable);
    }

    [Theory]
    [InlineData("string", "", "", "", FormFieldKind.Text)]
    [InlineData("string", "", "description", "", FormFieldKind.Paragraph)]
    [InlineData("string", "", "", "x:textarea", FormFieldKind.Paragraph)]
    [InlineData("number", "", "", "", FormFieldKind.Number)]
    [InlineData("date", "", "", "", FormFieldKind.Date)]
    [InlineData("datetime", "", "", "", FormFieldKind.DateTime)]
    [InlineData("resolution", "", "", "", FormFieldKind.Select)]
    [InlineData("array", "string", "labels", "", FormFieldKind.Labels)]
    [InlineData("array", "version", "", "", FormFieldKind.MultiSelect)]
    [InlineData("array", "attachment", "", "", FormFieldKind.Unsupported)]
    [InlineData("com.vendor:risk-matrix", "", "", "", FormFieldKind.Unsupported)]
    public void GivenTransitionFieldSchemas_WhenMapped_ThenTheKindFollowsTheSchema(
        string type,
        string items,
        string system,
        string custom,
        FormFieldKind expected)
    {
        var meta = new FieldMeta("f", "F", true, type, false, _choices, items, system, custom);

        var mapped = Assert.Single(FormFieldMapper.FromTransition([meta]));

        Assert.Equal(expected, mapped.Kind);
        Assert.True(mapped.Required);
    }

    private static EditField Edit(string id, EditFieldKind kind, bool required = false, string[]? operations = null)
    {
        return new EditField(id, id, required, "t", string.Empty, operations ?? ["set"], _choices, false, null, kind);
    }
}
