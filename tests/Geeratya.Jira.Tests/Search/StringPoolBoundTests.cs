using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Search;

[Trait("Os", "Independent")]
public sealed class StringPoolBoundTests
{
    [Fact]
    public void GivenEqualStrings_WhenSharing_ThenTheFirstInstanceIsReturned()
    {
        var pool = new StringPool();
        var first = pool.Share(new string('a', 5));

        var second = pool.Share(new string('a', 5));

        Assert.Same(first, second);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void GivenNull_WhenSharing_ThenNullIsReturned() => Assert.Null(new StringPool().Share(null));

    [Fact]
    public void GivenALongValue_WhenSharing_ThenItIsReturnedUnpooled()
    {
        var pool = new StringPool();
        var value = new string('x', StringPool.MaxLength + 1);

        Assert.Same(value, pool.Share(value));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void GivenAFullPool_WhenSharingANewValue_ThenThePoolIsEmptiedAndNeverExceedsItsCapacity()
    {
        var pool = new StringPool(3);

        for (var index = 0; index < 10; index++)
        {
            pool.Share($"value-{index}");
            Assert.True(pool.Count <= pool.Capacity);
        }

        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void GivenZeroCapacity_WhenCreated_ThenItHoldsOneEntry() => Assert.Equal(1, new StringPool(0).Capacity);
}
