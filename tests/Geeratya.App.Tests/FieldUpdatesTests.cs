using Geeratya.Core.Forms;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public class FieldUpdatesTests
{
    [Fact]
    public void GivenAChangedReporter_WhenConverted_ThenTheEditSetsTheAccountIdOfThePerson()
    {
        var change = new FieldChange(
            FormFieldIds.Reporter,
            FormFieldKind.User,
            FormValue.OfChosen(["acc-eve"]),
            FormValue.OfChosen(["acc-me"]));

        var update = Assert.Single(FieldUpdates.For([change]));

        Assert.Equal(FormFieldIds.Reporter, update.FieldId);
        Assert.Contains("acc-eve", update.ToElement().GetRawText());
    }
}
