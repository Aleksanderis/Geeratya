using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui.Tests.Shutdown;

[Trait("Os", "Independent")]
public sealed class UiInvokeGateTests
{
    [Fact]
    public void GivenOpenGate_WhenInvoking_ThenTheWorkRuns()
    {
        var gate = new UiInvokeGate();
        var runs = 0;

        var result = gate.TryInvoke(() => runs++);

        Assert.True(result);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void GivenClosedGate_WhenPosting_ThenTheWorkIsDroppedWithoutThrowing()
    {
        var gate = new UiInvokeGate();
        var runs = 0;

        gate.Close();

        var result = gate.TryInvoke(() => runs++);

        Assert.False(result);
        Assert.Equal(0, runs);
    }

    [Fact]
    public void GivenApplicationDisposedMeanwhile_WhenPosting_ThenTheExceptionIsSwallowed()
    {
        var gate = new UiInvokeGate();

        var result = gate.TryInvoke(() => throw new ObjectDisposedException("application"));

        Assert.False(result);
    }

    [Fact]
    public void GivenAnUnrelatedFailure_WhenPosting_ThenItIsNotHidden()
    {
        var gate = new UiInvokeGate();

        Assert.Throws<ArgumentException>(() => gate.TryInvoke(() => throw new ArgumentException("bug")));
    }
}
