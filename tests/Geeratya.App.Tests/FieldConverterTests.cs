using Geeratya.Core.Forms;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public class FieldConverterTests
{
    [Fact]
    public void GivenAChangedReporter_WhenConverted_ThenTheEditSetsTheAccountIdOfThePerson()
    {
        var change = new FieldChange(
            FormFieldIds.Reporter,
            FormFieldKind.User,
            FormValue.OfChosen(["acc-eve"]),
            FormValue.OfChosen(["acc-me"]));

        var update = Assert.Single(FieldConverter.ForEdit([change]));

        Assert.Equal(FormFieldIds.Reporter, update.FieldId);
        Assert.Contains("acc-eve", update.ToElement().GetRawText());
    }

    [Theory]
    [InlineData(FormFieldKind.Text, "  Title ", "\"Title\"")]
    [InlineData(FormFieldKind.Number, "2.5", "2.5")]
    [InlineData(FormFieldKind.Number, "3", "3")]
    [InlineData(FormFieldKind.Date, "2026-12-01", "\"2026-12-01\"")]
    [InlineData(FormFieldKind.DateTime, "2026-12-01 09:30", "\"2026-12-01T09:30:00.000")]
    public void GivenATypedValue_WhenConvertedForCreateAndEdit_ThenBothSendTheSameJson(
        FormFieldKind kind,
        string text,
        string expectedStart)
    {
        var change = new FieldChange("customfield_1", kind, FormValue.OfText(text));

        var edit = Assert.Single(FieldConverter.ForEdit([change])).ToElement().GetRawText();
        var create = FieldConverter.ForCreate(change).ToElement().GetRawText();

        Assert.Equal(edit, create);
        Assert.StartsWith(expectedStart, create, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FormFieldKind.Select, "{\"id\":\"10\"}")]
    [InlineData(FormFieldKind.User, "{\"accountId\":\"10\"}")]
    public void GivenAChosenValue_WhenConvertedForCreateAndEdit_ThenBothSendTheSameJson(
        FormFieldKind kind,
        string expected)
    {
        var change = new FieldChange("customfield_1", kind, FormValue.OfChosen(["10"]));

        Assert.Equal(expected, Assert.Single(FieldConverter.ForEdit([change])).ToElement().GetRawText());
        Assert.Equal(expected, FieldConverter.ForCreate(change).ToElement().GetRawText());
    }

    [Fact]
    public void GivenASetOfLabelsAndOptions_WhenConverted_ThenAnEditSendsAddsAndRemovesAndACreateTheWholeSet()
    {
        var labels = new FieldChange(
            "labels",
            FormFieldKind.Labels,
            FormValue.OfChosen(["a", "b"]),
            FormValue.OfChosen(["a", "c"]));
        var options = new FieldChange(
            "customfield_2",
            FormFieldKind.MultiSelect,
            FormValue.OfChosen(["1"]),
            FormValue.Empty);

        var edit = FieldConverter.ForEdit([labels, options]);

        Assert.All(edit, update => Assert.True(update.IsOperation));
        Assert.Equal("[{\"add\":\"b\"},{\"remove\":\"c\"}]", edit[0].ToElement().GetRawText());
        Assert.Equal("[\"a\",\"b\"]", FieldConverter.ForCreate(labels).ToElement().GetRawText());
        Assert.False(FieldConverter.ForCreate(options).IsOperation);
        Assert.Equal("[{\"id\":\"1\"}]", FieldConverter.ForCreate(options).ToElement().GetRawText());
    }

    [Fact]
    public void GivenAnEmptyValue_WhenConvertedForEdit_ThenTheFieldIsCleared()
    {
        var number = new FieldChange("customfield_1", FormFieldKind.Number, FormValue.Empty, FormValue.OfText("2"));
        var text = new FieldChange("customfield_3", FormFieldKind.Text, FormValue.OfText("  "), FormValue.OfText("x"));

        var updates = FieldConverter.ForEdit([number, text]);

        Assert.All(updates, update => Assert.Equal("null", update.ToElement().GetRawText()));
    }
}
