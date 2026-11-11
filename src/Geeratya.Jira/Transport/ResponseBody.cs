using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Geeratya.Jira.Transport;

/// <summary>
/// Reads a response body without a UTF-16 copy of it: a success body is deserialized straight from the UTF-8
/// stream (the serializer rents its buffers), an error body is cut at <see cref="MaxErrorBodyBytes"/>. Every read
/// is bounded by the token with <c>WaitAsync</c>, like the rest of the transport.
/// </summary>
internal static class ResponseBody
{
    /// <summary>Jira's error JSON is a few hundred bytes; the rest is an HTML page or noise the mapper never reads.</summary>
    internal const int MaxErrorBodyBytes = 8 * 1024;

    /// <summary>The whole body as text, for a call that may legitimately return none (a 204 or an empty 200).</summary>
    internal static async Task<string> ReadSmallAsync(HttpContent content, CancellationToken cancellationToken) =>
        await content.ReadAsStringAsync(cancellationToken).WaitAsync(cancellationToken);

    /// <summary>The first <see cref="MaxErrorBodyBytes"/> bytes of an error body, as text.</summary>
    internal static async Task<string> ReadErrorAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).WaitAsync(cancellationToken);
        var buffer = new byte[MaxErrorBodyBytes];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).AsTask()
                .WaitAsync(cancellationToken);

            if (read == 0)
            {
                break;
            }

            length += read;
        }

        // Jira sends UTF-8; a character cut by the cap becomes U+FFFD, which only reaches a snippet.
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    /// <summary>Deserializes a success body from the UTF-8 stream; null for a JSON <c>null</c>.</summary>
    internal static async Task<T?> ReadJsonAsync<T>(
        HttpContent content,
        JsonTypeInfo<T> info,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).WaitAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync(stream, info, cancellationToken).AsTask()
            .WaitAsync(cancellationToken);
    }
}
