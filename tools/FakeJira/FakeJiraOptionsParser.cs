namespace Geeratya.FakeJira;

/// <summary>Reads the command line into <see cref="FakeJiraOptions"/>.</summary>
public static class FakeJiraOptionsParser
{
    public const string IssuesFlag = "--issues";
    public const string LatencyFlag = "--latency-ms";
    public const string RateLimitFlag = "--rate-limit-every";
    public const string DropWriteFlag = "--drop-write-every";
    public const string PortFlag = "--port";
    public const string FixturesFlag = "--fixtures";

    private const string FixturesRelativePath = "tests/Geeratya.Jira.Tests/Fixtures";

    /// <summary>Parses the arguments; returns null and an error text when they are invalid.</summary>
    public static FakeJiraOptions? TryParse(string[] arguments, out string error)
    {
        error = string.Empty;
        var fixtures = FindFixtures(AppContext.BaseDirectory) ?? FindFixtures(Environment.CurrentDirectory);
        var port = 0;
        var issues = 0;
        var latency = 0;
        var rateLimitEvery = 0;
        var dropWriteEvery = 0;

        for (var index = 0; index < arguments.Length; index += 2)
        {
            var flag = arguments[index];

            if (index + 1 >= arguments.Length)
            {
                error = $"{flag} needs a value.";

                return null;
            }

            var value = arguments[index + 1];

            if (flag == FixturesFlag)
            {
                fixtures = value;

                continue;
            }

            if (!int.TryParse(value, out var number) || number < 0)
            {
                error = $"{flag} needs a non-negative number, got '{value}'.";

                return null;
            }

            switch (flag)
            {
                case PortFlag:
                    port = number;

                    break;
                case IssuesFlag:
                    issues = number;

                    break;
                case LatencyFlag:
                    latency = number;

                    break;
                case RateLimitFlag:
                    rateLimitEvery = number;

                    break;
                case DropWriteFlag:
                    dropWriteEvery = number;

                    break;
                default:
                    error = $"Unknown option {flag}.";

                    return null;
            }
        }

        if (fixtures is null || !Directory.Exists(fixtures))
        {
            error = $"Fixtures folder not found; pass {FixturesFlag} <dir>.";

            return null;
        }

        return new FakeJiraOptions(fixtures, port, issues, latency, rateLimitEvery, dropWriteEvery);
    }

    private static string? FindFixtures(string start)
    {
        var directory = new DirectoryInfo(start);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FixturesRelativePath);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
