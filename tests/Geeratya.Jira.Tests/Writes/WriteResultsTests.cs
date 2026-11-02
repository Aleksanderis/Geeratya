using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Writes;

public sealed class WriteResultsTests
{
    [Theory]
    [InlineData(JiraErrorKind.BadRequest, WriteErrorKind.Validation)]
    [InlineData(JiraErrorKind.Forbidden, WriteErrorKind.Permission)]
    [InlineData(JiraErrorKind.RateLimited, WriteErrorKind.RateLimited)]
    [InlineData(JiraErrorKind.HostRefused, WriteErrorKind.NotSent)]
    [InlineData(JiraErrorKind.ServerError, WriteErrorKind.UnknownOutcome)]
    public void GivenTransportError_WhenMapping_ThenWriteErrorKindMatches(JiraErrorKind kind, WriteErrorKind expected)
    {
        var error = WriteResults.Map(new JiraError(kind, null, "x"));

        Assert.Equal(expected, error.Kind);
    }

    [Fact]
    public void GivenSuccess_WhenReadingError_ThenInvalidOperation()
    {
        var result = WriteResults.Success(1);

        Assert.True(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void GivenFailure_WhenReadingValue_ThenInvalidOperation()
    {
        var result = WriteResults.Failure<int>(new WriteError(WriteErrorKind.Validation, null, "x", [], []));

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
