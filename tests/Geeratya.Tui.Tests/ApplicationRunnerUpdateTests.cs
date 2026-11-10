using Geeratya.App.Updates;

namespace Geeratya.Tui.Tests;

public class ApplicationRunnerUpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenTheUpdateWord_WhenRunning_ThenTheCommandRunsOnThePlainConsoleAndItsExitCodeIsReturned(bool check)
    {
        using var writer = new StringWriter();
        var command = new RecordingUpdateCommand(1);
        string[] arguments = check ? ["update", "--check"] : ["update"];

        var exitCode = new ApplicationRunner(new RunnerContext(writer, false), update: command).Run(arguments);

        Assert.Equal(1, exitCode);
        Assert.Equal(check, command.CheckOnly);
        Assert.Equal("ran", writer.ToString().Trim());
    }

    [Fact]
    public void GivenAnUnknownUpdateArgument_WhenRunning_ThenItIsNamedAndTheCommandDoesNotRun()
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();
        var command = new RecordingUpdateCommand(0);

        var exitCode = new ApplicationRunner(new RunnerContext(writer, false, Error: error), update: command)
            .Run(["update", "--now"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("'--now'", error.ToString(), StringComparison.Ordinal);
        Assert.Null(command.CheckOnly);
    }

    [Fact]
    public void GivenUpdateHelp_WhenRunning_ThenTheUsageMentionsTheCommand()
    {
        using var writer = new StringWriter();
        var command = new RecordingUpdateCommand(0);

        var exitCode = new ApplicationRunner(new RunnerContext(writer, false), update: command).Run(["update", "--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("update [--check]", writer.ToString(), StringComparison.Ordinal);
        Assert.Null(command.CheckOnly);
    }

    private sealed class RecordingUpdateCommand : IUpdateCommand
    {
        private readonly int _exitCode;

        public RecordingUpdateCommand(int exitCode)
        {
            _exitCode = exitCode;
        }

        public bool? CheckOnly { get; private set; }

        public Task<int> RunAsync(bool checkOnly, TextWriter output, CancellationToken cancellationToken)
        {
            CheckOnly = checkOnly;
            output.WriteLine("ran");

            return Task.FromResult(_exitCode);
        }
    }
}
