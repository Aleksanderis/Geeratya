using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Browser;

/// <summary>
/// Opens URLs through the opener program of the operating system. Only http and https URLs without control
/// characters are passed on, as one list argument: no shell is involved, so the URL is never parsed as a command.
/// </summary>
public sealed class ProcessBrowserLauncher : IBrowserLauncher
{
    private const string NoOpenerMessage = "no browser opener found: copy the URL";
    private const string RefusedMessage = "only http and https links can be opened";

    private readonly OperatingSystemKind _operatingSystem;
    private readonly IProcessRunner _processRunner;

    public ProcessBrowserLauncher(OperatingSystemKind operatingSystem, IProcessRunner processRunner)
    {
        _operatingSystem = operatingSystem;
        _processRunner = processRunner;
    }

    /// <summary>The opener for the system, or null when the system has none.</summary>
    public static BrowserCommand? CommandFor(OperatingSystemKind operatingSystem, string url)
    {
        return operatingSystem switch
        {
            OperatingSystemKind.Linux => new BrowserCommand("xdg-open", [url]),
            OperatingSystemKind.MacOs => new BrowserCommand("open", [url]),
            OperatingSystemKind.Windows => new BrowserCommand("rundll32", ["url.dll,FileProtocolHandler", url]),
            _ => null,
        };
    }

    public async Task<BrowserLaunchResult> OpenAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!IsOpenable(url))
        {
            return BrowserLaunchResult.Failure(RefusedMessage);
        }

        var command = CommandFor(_operatingSystem, url.AbsoluteUri);

        if (command is null)
        {
            return BrowserLaunchResult.Failure(NoOpenerMessage);
        }

        try
        {
            var exitCode = await _processRunner.RunDetachedAsync(command.Program, command.Arguments, cancellationToken);

            return exitCode == 0
                ? BrowserLaunchResult.Success
                : BrowserLaunchResult.Failure($"{command.Program} failed with exit code {exitCode}");
        }
        catch (ProgramNotFoundException)
        {
            return BrowserLaunchResult.Failure(NoOpenerMessage);
        }
        catch (TimeoutException)
        {
            return BrowserLaunchResult.Failure($"{command.Program} did not finish in time");
        }
    }

    private static bool IsOpenable(Uri url)
    {
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        return !url.OriginalString.Any(char.IsControl);
    }
}
