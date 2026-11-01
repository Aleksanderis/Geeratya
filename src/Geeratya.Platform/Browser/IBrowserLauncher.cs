namespace Geeratya.Platform.Browser;

/// <summary>Opens a URL in the default browser of the system; a seam so tests never start a browser.</summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Opens the URL. Never throws for an unusable opener or URL: the result carries a message for the status line.
    /// </summary>
    Task<BrowserLaunchResult> OpenAsync(Uri url, CancellationToken cancellationToken);
}
