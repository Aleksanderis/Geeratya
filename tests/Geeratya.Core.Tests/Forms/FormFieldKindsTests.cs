using Geeratya.Core.Forms;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class FormFieldKindsTests
{
    [Theory]
    [InlineData(FormFieldKind.Date)]
    [InlineData(FormFieldKind.DateTime)]
    [InlineData(FormFieldKind.Text)]
    [InlineData(FormFieldKind.Number)]
    public void GivenAField_WhenNamed_ThenTheFormatOfItsLabelIsDropped(FormFieldKind kind)
    {
        var field = FormField.Create("customfield_1", "Due date", kind, false, []);

        Assert.Equal("Due date", FormFieldKinds.Named(field));
    }

    [Theory]
    [InlineData(FormFieldIds.Summary, FormFieldKind.Text, false)]
    [InlineData(FormFieldIds.Description, FormFieldKind.Paragraph, false)]
    [InlineData("customfield_1", FormFieldKind.Paragraph, false)]
    [InlineData("customfield_1", FormFieldKind.Text, true)]
    [InlineData("customfield_1", FormFieldKind.Number, true)]
    [InlineData("customfield_1", FormFieldKind.Date, true)]
    [InlineData("customfield_1", FormFieldKind.DateTime, true)]
    [InlineData("customfield_1", FormFieldKind.Select, true)]
    [InlineData("customfield_1", FormFieldKind.User, true)]
    public void GivenAField_WhenAskedForAnEditBox_ThenPickersAndTypedFieldsHaveOneButThePanesDoNot(
        string id,
        FormFieldKind kind,
        bool expected)
    {
        var field = FormField.Create(id, "A", kind, false, []);

        Assert.Equal(expected, FormFieldKinds.HasEditBox(field));
    }
}
