using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Geeratya.App.Tests.Fakes;

/// <summary>
/// An in-process stand-in for GitHub's release endpoint and the file downloads: answers by URL, records every
/// request, and can fail a URL like an interrupted download. No socket is opened.
/// </summary>
public sealed class FakeReleaseServer : HttpMessageHandler
{
    public const string BaseUrl = "https://fake.test/releases";
    public const string DownloadBase = "https://fake.test/download";

    private readonly Dictionary<string, byte[]> _bodies = [];
    private readonly HashSet<string> _interrupted = [];
    private readonly Dictionary<string, HttpStatusCode> _statuses = [];

    public List<string> Requests { get; } = [];

    public List<HttpRequestHeaders> Headers { get; } = [];

    public static string DownloadUrl(string name)
    {
        return $"{DownloadBase}/{name}";
    }

    /// <summary>Serves the latest-release JSON naming <paramref name="assets"/> under <see cref="DownloadBase"/>.</summary>
    public void ServeRelease(string tag, params string[] assets)
    {
        var entries = assets.Select(name => $"{{\"name\":\"{name}\",\"browser_download_url\":\"{DownloadUrl(name)}\"}}");

        _bodies[$"{BaseUrl}/latest"] = Encoding.UTF8.GetBytes($"{{\"tag_name\":\"{tag}\",\"assets\":[{string.Join(',', entries)}]}}");
    }

    public void Serve(string url, byte[] body)
    {
        _bodies[url] = body;
    }

    public void Serve(string url, string text)
    {
        _bodies[url] = Encoding.UTF8.GetBytes(text);
    }

    public void Answer(string url, HttpStatusCode status)
    {
        _statuses[url] = status;
    }

    public void Interrupt(string url)
    {
        _interrupted.Add(url);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();

        Requests.Add(url);
        Headers.Add(request.Headers);

        if (_interrupted.Contains(url))
        {
            throw new HttpRequestException("the connection was reset");
        }

        if (_statuses.TryGetValue(url, out var status))
        {
            return Task.FromResult(new HttpResponseMessage(status));
        }

        if (_bodies.TryGetValue(url, out var body))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
