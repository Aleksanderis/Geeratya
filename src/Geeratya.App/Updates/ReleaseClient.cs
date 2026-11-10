using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Geeratya.Core;
using Geeratya.Core.Updates;

namespace Geeratya.App.Updates;

/// <summary>
/// Asks GitHub for the latest release of Geeratya and downloads its files. No account data and no token go with a
/// request, only a <c>User-Agent</c> (<c>Geeratya/&lt;version&gt;</c>). The HTTP client is built on first use, so
/// creating this object costs nothing at start. It follows the system proxy settings (the environment variables
/// everywhere, the system settings on Windows) and offers the signed-in Windows account to a proxy that asks.
/// </summary>
public sealed class ReleaseClient : IDisposable
{
    /// <summary>The default release API of the repository; <c>latest</c> is appended.</summary>
    public const string DefaultReleasesUrl = "https://api.github.com/repos/Aleksanderis/Geeratya/releases";

    /// <summary>The environment variable that replaces <see cref="DefaultReleasesUrl"/> (tests, mirrors).</summary>
    public const string ReleasesUrlVariable = "GEERATYA_RELEASES_URL";

    private static readonly TimeSpan _lookupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _downloadTimeout = TimeSpan.FromMinutes(10);

    private readonly string _releasesUrl;
    private readonly HttpMessageHandler? _handler;
    private HttpClient? _client;

    /// <param name="releasesUrl">The release API base; <c>latest</c> is appended.</param>
    /// <param name="handler">A message handler to use instead of the real network (tests).</param>
    public ReleaseClient(string? releasesUrl = null, HttpMessageHandler? handler = null)
    {
        _releasesUrl = (releasesUrl ?? DefaultReleasesUrl).TrimEnd('/');
        _handler = handler;
    }

    /// <summary>
    /// Asks for the latest release; a failure is a message, never an exception (except cancellation).
    /// </summary>
    public async Task<ReleaseLookup> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_lookupTimeout);

        try
        {
            using var response = await Client().GetAsync($"{_releasesUrl}/latest", timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return ReleaseLookup.Failed(StatusText(response.StatusCode));
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            var release = await JsonSerializer.DeserializeAsync(
                body,
                UpdateJsonContext.Default.GitHubRelease,
                timeout.Token);

            return Map(release);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ReleaseLookup.Failed("GitHub did not answer in time");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return ReleaseLookup.Failed(exception.Message);
        }
    }

    /// <summary>
    /// Downloads <paramref name="url"/> to the file <paramref name="destination"/>. Throws
    /// <see cref="HttpRequestException"/> (also for a non-success status), <see cref="IOException"/> or
    /// <see cref="TimeoutException"/>.
    /// </summary>
    public async Task DownloadAsync(string url, string destination, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_downloadTimeout);

        try
        {
            using var response = await Client().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

            await source.CopyToAsync(target, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("the download took too long");
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private static ReleaseLookup Map(GitHubRelease? release)
    {
        var version = ReleaseVersion.Parse(release?.TagName);

        if (release?.TagName is not { } tag || version is null)
        {
            return ReleaseLookup.Failed("the latest release has no version tag");
        }

        var assets = (release.Assets ?? [])
            .Where(asset => asset.Name is not null && asset.DownloadUrl is not null)
            .Select(asset => new ReleaseAsset(asset.Name!, asset.DownloadUrl!))
            .ToList();

        return ReleaseLookup.Found(new ReleaseInfo(tag, version.Text, assets));
    }

    private static string StatusText(HttpStatusCode status)
    {
        return status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            ? $"GitHub answered {(int)status} (a rate limit? try again later)"
            : $"GitHub answered {(int)status} {status}";
    }

    private HttpClient Client()
    {
        if (_client is not null)
        {
            return _client;
        }

        _client = new HttpClient(_handler ?? CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Geeratya", ApplicationInfo.Version));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return _client;
    }

    private static SocketsHttpHandler CreateHandler()
    {
        // The default proxy reads the system settings; a corporate proxy often wants the Windows sign-in.
        return new SocketsHttpHandler
        {
            Proxy = HttpClient.DefaultProxy,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            ConnectTimeout = _lookupTimeout,
        };
    }
}
