using Geeratya.Core.Lists;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Lists;

[Trait("Os", "Independent")]
public sealed class IssueTypeRoleTests
{
    [Theory]
    [InlineData("Epic", 1, false, TextRole.TypeEpic)]
    [InlineData("Story", 0, false, TextRole.TypeStory)]
    [InlineData("Bug", 0, false, TextRole.TypeBug)]
    [InlineData("Task", 0, false, TextRole.TypeTask)]
    [InlineData("Sub-task", -1, true, TextRole.TypeSubtask)]
    [InlineData("Spike", 0, false, TextRole.Body)]
    public void GivenAnIssueType_WhenAskingForItsRole_ThenItFollowsTheMarker(
        string name,
        int level,
        bool subtask,
        TextRole expected)
    {
        Assert.Equal(expected, IssueTypeRole.For(new IssueTypeSummary("1", name, level, subtask)));
    }
}
