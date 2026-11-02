using Geeratya.Core.State;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Collects posted commands in a list the test reads.</summary>
public sealed class ListCommandSink : ICommandSink
{
    private readonly List<Command> _commands;

    public ListCommandSink(List<Command> commands)
    {
        _commands = commands;
    }

    public void Post(Command command)
    {
        lock (_commands)
        {
            _commands.Add(command);
        }
    }
}
