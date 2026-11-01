using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Views;

public class ViewDefinitionTests
{
    [Fact]
    public void Same_source_can_use_different_layouts()
    {
        var list = new ViewDefinition(ViewSourceKind.Board, "42", ViewLayout.List);
        var columns = list with { Layout = ViewLayout.Columns };

        Assert.Equal(list.Source, columns.Source);
        Assert.Equal("42", columns.SourceValue);
        Assert.Equal(ViewLayout.Columns, columns.Layout);
        Assert.NotEqual(list, columns);
    }
}
