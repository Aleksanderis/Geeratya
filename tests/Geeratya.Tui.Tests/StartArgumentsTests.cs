using Geeratya.Core.Startup;

namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public class StartArgumentsTests
{
    private static readonly string[] _valueFlags = ["--profile", "--board", "--backlog", "--jql", "--filter", "--size"];

    [Theory]
    [InlineData("GEN-123", StartTargetKind.Issue, "GEN-123")]
    [InlineData("gen-7", StartTargetKind.Issue, "GEN-7")]
    public void GivenAnIssueKey_WhenParsed_ThenItIsTheIssueTarget(string argument, StartTargetKind kind, string value)
    {
        var options = StartArguments.Parse([argument], _valueFlags, out var error);

        Assert.Null(error);
        Assert.Equal(new StartTarget(kind, value), options!.Target);
    }

    [Theory]
    [InlineData("--board", StartTargetKind.Board)]
    [InlineData("--backlog", StartTargetKind.Backlog)]
    [InlineData("--jql", StartTargetKind.Jql)]
    [InlineData("--filter", StartTargetKind.Filter)]
    public void GivenATargetFlag_WhenParsed_ThenTheValueIsTheTargetText(string flag, StartTargetKind kind)
    {
        var options = StartArguments.Parse([flag, "assignee = currentUser()"], _valueFlags, out var error);

        Assert.Null(error);
        Assert.Equal(new StartTarget(kind, "assignee = currentUser()"), options!.Target);
    }

    [Fact]
    public void GivenAProfileAndNoTarget_WhenParsed_ThenOnlyTheProfileIsSet()
    {
        var options = StartArguments.Parse(["--profile", "home"], _valueFlags, out _);

        Assert.Equal("home", options!.Profile);
        Assert.Null(options.Target);
    }

    [Fact]
    public void GivenAValueThatLooksLikeAKey_WhenItBelongsToAnotherFlag_ThenItIsNotATarget()
    {
        var options = StartArguments.Parse(["--profile", "GEN-1"], _valueFlags, out _);

        Assert.Null(options!.Target);
    }

    [Fact]
    public void GivenNoArguments_WhenParsed_ThenThereIsNoTarget()
    {
        var options = StartArguments.Parse([], _valueFlags, out var error);

        Assert.Null(error);
        Assert.Null(options!.Target);
        Assert.Null(options.Profile);
    }

    [Fact]
    public void GivenTwoTargets_WhenParsed_ThenItIsAnError()
    {
        var options = StartArguments.Parse(["GEN-1", "--board", "GCM"], _valueFlags, out var error);

        Assert.Null(options);
        Assert.Contains("one start target", error);
    }

    [Fact]
    public void GivenATargetFlagWithoutValue_WhenParsed_ThenItIsAnError()
    {
        var options = StartArguments.Parse(["--jql"], _valueFlags, out var error);

        Assert.Null(options);
        Assert.Equal("--jql needs a value", error);
    }

    [Fact]
    public void GivenATargetFlagWithoutValue_WhenRunning_ThenExitsTwoWithUsage()
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(new RunnerContext(writer, true, Error: error)).Run(["--board"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("--board needs a value", error.ToString());
    }

    [Fact]
    public void GivenTheUsage_WhenRead_ThenItNamesEveryStartTarget()
    {
        Assert.Contains("--board", ApplicationRunner.Usage);
        Assert.Contains("--backlog", ApplicationRunner.Usage);
        Assert.Contains("--jql", ApplicationRunner.Usage);
        Assert.Contains("--filter", ApplicationRunner.Usage);
        Assert.Contains("GEN-123", ApplicationRunner.Usage);
    }
}
