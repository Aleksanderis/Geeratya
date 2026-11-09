using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

/// <summary>The stable colour of a person's initials chip (#871).</summary>
[Trait("Os", "Independent")]
public class PersonChipsTests
{
    [Fact]
    public void GivenTheSameName_WhenPicking_ThenTheRoleIsTheSameWhateverTheCaseAndPadding()
    {
        Assert.Equal(PersonChips.For("Jane Doe"), PersonChips.For("  JANE doe "));
    }

    [Fact]
    public void GivenManyNames_WhenPicking_ThenSeveralChipColoursAreUsedAndAllAreChipRoles()
    {
        string[] names = ["Jane Doe", "John Smith", "Rita Reporter", "Ann Assignee", "Generated Writer", "Bob Builder", "Carol King", "Dan Brown", "Eve Adams", "Frank Ocean"];

        var roles = names.Select(PersonChips.For).Distinct().ToList();

        Assert.True(roles.Count >= 3);
        Assert.All(roles, role => Assert.InRange(role, TextRole.LabelChip0, TextRole.LabelChip4));
    }

    [Theory]
    [InlineData("JD", 2, " JD ")]
    [InlineData("M", 2, " M  ")]
    [InlineData("", 2, "    ")]
    public void GivenInitials_WhenBuildingTheText_ThenItKeepsTheCellWidthWithOnePaddingCellEachSide(string initials, int cells, string expected)
    {
        Assert.Equal(expected, PersonChips.Text(initials, cells));
    }
}
