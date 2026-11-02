using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Writes;

[Trait("Os", "Independent")]
public sealed class ReadAfterWriteTests
{
    [Fact]
    public async Task GivenUnknownOutcome_WhenReloading_ThenReadRunsOnce()
    {
        var write = WriteResults.Failure<WriteAccepted>(Error(WriteErrorKind.Timeout));
        var counter = new ReadCounter();

        var reloaded = await ReadAfterWrite.ReloadIfUnknownAsync(write, counter.ReadAsync, Cancel());

        Assert.NotNull(reloaded);
        Assert.Equal(1, counter.Reads);
    }

    [Theory]
    [InlineData(WriteErrorKind.Validation)]
    [InlineData(WriteErrorKind.Permission)]
    [InlineData(WriteErrorKind.Conflict)]
    [InlineData(WriteErrorKind.RateLimited)]
    public async Task GivenCertainFailure_WhenReloading_ThenNoReadHappens(WriteErrorKind kind)
    {
        var write = WriteResults.Failure<WriteAccepted>(Error(kind));
        var counter = new ReadCounter();

        var reloaded = await ReadAfterWrite.ReloadIfUnknownAsync(write, counter.ReadAsync, Cancel());

        Assert.Null(reloaded);
        Assert.Equal(0, counter.Reads);
    }

    [Fact]
    public async Task GivenSuccess_WhenReloading_ThenNoReadHappens()
    {
        var write = WriteResults.Success(new WriteAccepted());
        var counter = new ReadCounter();

        var reloaded = await ReadAfterWrite.ReloadIfUnknownAsync(write, counter.ReadAsync, Cancel());

        Assert.Null(reloaded);
        Assert.Equal(0, counter.Reads);
    }

    private static CancellationToken Cancel() => CancellationToken.None;

    private static WriteError Error(WriteErrorKind kind) => new(kind, null, "x", [], []);

    private sealed class ReadCounter
    {
        public int Reads { get; private set; }

        public Task<JiraResult<int>> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;

            return Task.FromResult(JiraResults.Success(1));
        }
    }
}
