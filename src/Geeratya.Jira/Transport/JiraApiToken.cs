namespace Geeratya.Jira.Transport;

/// <summary>An API token. <c>ToString()</c> is redacted; only the transport can read the value.</summary>
public sealed class JiraApiToken
{
    internal const string RedactedText = "[redacted]";

    public JiraApiToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    internal string Value { get; }

    public override string ToString() => RedactedText;
}
