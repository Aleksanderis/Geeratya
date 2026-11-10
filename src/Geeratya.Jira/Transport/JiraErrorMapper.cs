using System.Net;
using System.Text.Json;

namespace Geeratya.Jira.Transport;

/// <summary>Maps a failed status and body to a <see cref="JiraError"/>. The status decides the kind.</summary>
public static class JiraErrorMapper
{
    private const int MaxSnippetLength = 200;
    private const string HtmlPageMessage = "Jira returned an error page.";

    private static readonly string[] _problemDetailNames = ["detail", "title"];

    public static JiraError Map(HttpStatusCode status, string body, TimeSpan? retryAfter)
    {
        var code = (int)status;
        var message = ExtractMessage(body);

        return code switch
        {
            400 => new JiraError(JiraErrorKind.BadRequest, code, Or(message, "Jira rejected the request."))
            {
                FieldErrors = ExtractFieldErrors(body),
                Messages = ExtractMessages(body),
            },
            409 => new JiraError(JiraErrorKind.Conflict, code, Or(message, "Jira refused the change."))
            {
                FieldErrors = ExtractFieldErrors(body),
                Messages = ExtractMessages(body),
            },
            401 => new JiraError(JiraErrorKind.Unauthorized, code, "Bad or expired credentials (email or API token)."),
            403 => new JiraError(JiraErrorKind.Forbidden, code, Or(message, "No permission.")),
            407 => ProxyAuthenticationRequired(),
            404 or 410 => new JiraError(JiraErrorKind.NotFound, code, "Not found or no access."),
            429 => new JiraError(JiraErrorKind.RateLimited, code, "Rate limited by Jira.", retryAfter),
            >= 500 => new JiraError(JiraErrorKind.ServerError, code, Or(message, "Jira server error."), retryAfter),
            _ => new JiraError(JiraErrorKind.Unexpected, code, Or(message, $"Unexpected status {code}.")),
        };
    }

    /// <summary>The proxy, not Jira, refused the call.</summary>
    public static JiraError ProxyAuthenticationRequired() =>
        new(JiraErrorKind.Unexpected, 407, "The proxy requires authentication (407); check the system proxy settings.");

    /// <summary>True when a failed HTTPS tunnel through the proxy ended in 407.</summary>
    public static bool IsProxyAuthentication(HttpRequestException exception) =>
        exception.StatusCode == HttpStatusCode.ProxyAuthenticationRequired;

    /// <summary>The v3 <c>errors</c> object as field id to message; empty when the body has none.</summary>
    public static IReadOnlyDictionary<string, string> ExtractFieldErrors(string body)
    {
        var result = new Dictionary<string, string>();

        if (TryParseRoot(body, out var document))
        {
            using (document)
            {
                ReadFieldErrors(document.RootElement, result);
            }
        }

        return result;
    }

    /// <summary>Every non-empty <c>errorMessages</c> entry; empty when the body has none.</summary>
    public static IReadOnlyList<string> ExtractMessages(string body)
    {
        var result = new List<string>();

        if (TryParseRoot(body, out var document))
        {
            using (document)
            {
                ReadMessages(document.RootElement, result);
            }
        }

        return result;
    }

    /// <summary>Tries the two JSON error shapes, then a short plain-text snippet. HTML is never shown.</summary>
    public static string ExtractMessage(string body)
    {
        var trimmed = body.Trim();

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed[0] == '<')
        {
            return HtmlPageMessage;
        }

        if (trimmed[0] != '{')
        {
            return Truncate(trimmed);
        }

        return TryExtractJsonMessage(trimmed) ?? string.Empty;
    }

    private static bool TryParseRoot(string body, out JsonDocument document)
    {
        document = null!;
        var trimmed = body.Trim();

        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(trimmed);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ReadFieldErrors(JsonElement root, Dictionary<string, string> result)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.String && field.Value.GetString() is { Length: > 0 } text)
            {
                result[field.Name] = Truncate(text);
            }
        }
    }

    private static void ReadMessages(JsonElement root, List<string> result)
    {
        if (!root.TryGetProperty("errorMessages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } text)
            {
                result.Add(Truncate(text));
            }
        }
    }

    private static string? TryExtractJsonMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return FromErrorMessages(root) ?? FromErrors(root) ?? FromProblemDetails(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FromErrorMessages(JsonElement root)
    {
        if (!root.TryGetProperty("errorMessages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } text)
            {
                return Truncate(text);
            }
        }

        return null;
    }

    private static string? FromErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.String)
            {
                return Truncate($"{field.Name}: {field.Value.GetString()}");
            }
        }

        return null;
    }

    private static string? FromProblemDetails(JsonElement root)
    {
        foreach (var name in _problemDetailNames)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return Truncate(value.GetString() ?? string.Empty);
            }
        }

        return null;
    }

    private static string Or(string message, string fallback) => message.Length == 0 ? fallback : message;

    private static string Truncate(string text) =>
        text.Length <= MaxSnippetLength ? text : $"{text[..MaxSnippetLength]}...";
}
