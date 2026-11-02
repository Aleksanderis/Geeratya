using Geeratya.Core.Forms;
using Geeratya.Core.Links;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Links.Models;

namespace Geeratya.Core.Tests.Links;

[Trait("Os", "Independent")]
public class LinkFormTests
{
    private static readonly IssueLinkType _blocks = new("10000", "Blocks", "is blocked by", "blocks");
    private static readonly IssueLinkType _relates = new("10003", "Relates", "relates to", "relates to");

    [Fact]
    public void GivenTypes_WhenBuildingTheFields_ThenEachSideIsAChoiceAndASymmetricTypeIsOne()
    {
        var fields = LinkFormFields.From([_blocks, _relates]);

        var type = fields[0];
        Assert.Equal(LinkFormFields.TypeFieldId, type.Id);
        Assert.Equal(FormFieldKind.Select, type.Kind);
        Assert.True(type.Required);
        Assert.Equal(["blocks", "is blocked by", "relates to"], type.Options.Select(option => option.Label));
        Assert.Equal(LinkFormFields.IssueFieldId, fields[1].Id);
        Assert.Equal(FormFieldKind.Text, fields[1].Kind);
        Assert.True(fields[1].Required);
    }

    [Theory]
    [InlineData("Outward")]
    [InlineData("Inward")]
    public void GivenAChoice_WhenTheOptionIdIsRead_ThenTypeSideAndWordingComeBack(string side)
    {
        var direction = Enum.Parse<IssueLinkDirection>(side);

        var id = LinkOptionId.Of("10000", direction, "is blocked by / maybe");

        Assert.True(LinkOptionId.TryParse(id, out var choice));
        Assert.Equal(new LinkChoice("10000", direction, "is blocked by / maybe"), choice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("10000")]
    [InlineData("10000/up/blocks")]
    [InlineData("/out/blocks")]
    public void GivenAnIdThatIsNotAnOptionId_WhenParsing_ThenItIsRefused(string id)
    {
        Assert.False(LinkOptionId.TryParse(id, out _));
    }

    [Fact]
    public void GivenTheOutwardChoice_WhenBuildingTheRequest_ThenThisIssueIsTheOutwardSide()
    {
        var request = Request("10000", IssueLinkDirection.Outward, "blocks", "gcm-5");

        Assert.Equal("A-1", request.OutwardKey);
        Assert.Equal("GCM-5", request.InwardKey);
        Assert.Equal("10000", request.TypeId);
        Assert.Equal("Linked A-1 blocks GCM-5", request.DoneText);
    }

    [Fact]
    public void GivenTheInwardChoice_WhenBuildingTheRequest_ThenThisIssueIsTheInwardSide()
    {
        var request = Request("10000", IssueLinkDirection.Inward, "is blocked by", "  GCM-5 ");

        Assert.Equal("GCM-5", request.OutwardKey);
        Assert.Equal("A-1", request.InwardKey);
        Assert.Equal("A-1 is blocked by GCM-5", request.Sentence);
        Assert.Equal(["A-1", "GCM-5"], request.Keys);
    }

    [Fact]
    public void GivenNoChoice_WhenBuildingTheRequest_ThenTheTypeIsEmptySoTheClientRefusesIt()
    {
        var request = Assert.IsType<LinkIssuesRequest>(new LinkFormTarget("A-1").Request([
            new FieldChange(LinkFormFields.IssueFieldId, FormFieldKind.Text, FormValue.OfText("A-2")),
        ]));

        Assert.Equal(string.Empty, request.TypeId);
    }

    [Fact]
    public void GivenALinkWrite_WhenAskedForRowChanges_ThenThereAreNone()
    {
        var request = Request("10000", IssueLinkDirection.Outward, "blocks", "A-2");

        Assert.Null(request.Plan(null!));
    }

    private static LinkIssuesRequest Request(string typeId, IssueLinkDirection direction, string text, string other)
    {
        var choice = FormValue.OfChosen([LinkOptionId.Of(typeId, direction, text)]);

        return Assert.IsType<LinkIssuesRequest>(new LinkFormTarget("A-1").Request([
            new FieldChange(LinkFormFields.TypeFieldId, FormFieldKind.Select, choice),
            new FieldChange(LinkFormFields.IssueFieldId, FormFieldKind.Text, FormValue.OfText(other)),
        ]));
    }
}
