using System.Net;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Transport;

[Trait("Os", "Independent")]
public class JiraErrorMapperTests
{
    [Fact]
    public void GivenProblemDetailsBody_WhenExtractingMessage_ThenDetailIsUsed()
    {
        var body = "{\"detail\":\"maxResults too large\",\"status\":400,\"title\":\"Bad Request\"}";

        Assert.Equal("maxResults too large", JiraErrorMapper.ExtractMessage(body));
    }

    [Fact]
    public void GivenFieldErrorsBody_WhenExtractingMessage_ThenFieldAndMessageAreUsed()
    {
        var body = "{\"errorMessages\":[],\"errors\":{\"issuetype\":\"bad type\"}}";

        Assert.Equal("issuetype: bad type", JiraErrorMapper.ExtractMessage(body));
    }

    [Fact]
    public void GivenPlainTextBody_WhenExtractingMessage_ThenTextIsReturned()
    {
        Assert.Equal("XSRF check failed", JiraErrorMapper.ExtractMessage("XSRF check failed"));
    }

    [Fact]
    public void GivenVeryLongText_WhenExtractingMessage_ThenItIsTruncated()
    {
        var message = JiraErrorMapper.ExtractMessage(new string('a', 5000));

        Assert.True(message.Length < 300);
    }

    [Fact]
    public void GivenBrokenJson_WhenExtractingMessage_ThenEmptyMessageNoThrow()
    {
        Assert.Equal(string.Empty, JiraErrorMapper.ExtractMessage("{not json"));
    }

    [Fact]
    public void GivenStatus429WithRetryAfter_WhenMapping_ThenRetryAfterIsKept()
    {
        var error = JiraErrorMapper.Map((HttpStatusCode)429, string.Empty, TimeSpan.FromSeconds(3));

        Assert.Equal(JiraErrorKind.RateLimited, error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(3), error.RetryAfter);
    }

    [Fact]
    public void GivenUnexpectedStatus_WhenMapping_ThenUnexpectedKind()
    {
        Assert.Equal(JiraErrorKind.Unexpected, JiraErrorMapper.Map((HttpStatusCode)418, string.Empty, null).Kind);
    }
}
