using Geeratya.FakeJira;

const string usageText =
    "usage: fakejira [--fixtures <dir>] [--port <n>] [--issues <n>] [--latency-ms <n>] [--rate-limit-every <n>] [--drop-write-every <n>]";

var options = FakeJiraOptionsParser.TryParse(args, out var error);

if (options is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(usageText);

    return 2;
}

using var server = new FakeJiraServer(options);
// No disposal needed: the wait handle is never used, and the handlers below outlive Main.
var stop = new ManualResetEventSlim();
Console.CancelKeyPress += (_, eventArguments) =>
{
    eventArguments.Cancel = true;
    stop.Set();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Set();

Console.Out.WriteLine(server.Start());
Console.Out.Flush();
stop.Wait();

return 0;
