using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ViewDefinitionTests
{
    [Fact]
    public void GivenBoardSource_WhenSwitchingLayout_ThenSourceStaysAndLayoutChanges()
    {
        var list = new ViewDefinition(ViewSourceKind.Board, "42", ViewLayout.List);

        var columns = list with { Layout = ViewLayout.Columns };

        Assert.Equal(ViewSourceKind.Board, columns.Source);
        Assert.Equal("42", columns.SourceValue);
        Assert.Equal(ViewLayout.Columns, columns.Layout);
        Assert.Equal(ViewLayout.List, list.Layout);
    }
}
