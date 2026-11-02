using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Where background work hands its results over: a thread-safe entry that applies the command to the state on the
/// host's own thread (in the shell, the UI thread). Implemented by the host; tests record the commands.
/// </summary>
public interface ICommandSink
{
    void Post(Command command);
}
