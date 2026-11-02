using System.Collections.Concurrent;
using Geeratya.Core.State;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Collects posted commands in a queue the test reads.</summary>
public sealed class QueueCommandSink : ICommandSink
{
    private readonly ConcurrentQueue<Command> _commands;

    public QueueCommandSink(ConcurrentQueue<Command> commands)
    {
        _commands = commands;
    }

    public void Post(Command command)
    {
        _commands.Enqueue(command);
    }
}
