using Geeratya.Core.Jump;
using Geeratya.Core.Startup;

namespace Geeratya.Tui;

/// <summary>
/// Reads the start target of the command line: a positional issue key, or one of <c>--board</c>, <c>--backlog</c>,
/// <c>--jql</c> and <c>--filter</c> with its value (at most one), and <c>--profile</c>.
/// </summary>
public static class StartArguments
{
    public const string BoardFlag = "--board";
    public const string BacklogFlag = "--backlog";
    public const string JqlFlag = "--jql";
    public const string FilterFlag = "--filter";
    public const string ProfileFlag = "--profile";

    /// <summary>The flags that name a target; each takes a value.</summary>
    public static readonly string[] TargetFlags = [BoardFlag, BacklogFlag, JqlFlag, FilterFlag];

    /// <summary>True when <paramref name="argument"/> is a positional issue key such as <c>GEN-123</c>.</summary>
    public static bool IsIssueKey(string argument)
    {
        return !argument.StartsWith('-') && JumpKey.Parse(argument, null).Kind == JumpParseKind.Key;
    }

    /// <summary>
    /// Reads the options out of arguments the runner already checked for unknown words. <paramref name="valueFlags"/>
    /// are every flag that takes a value, so a value is never taken for an issue key.
    /// </summary>
    public static StartOptions? Parse(string[] arguments, string[] valueFlags, out string? error)
    {
        error = null;
        var targets = new List<StartTarget>();
        string? profile = null;

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (Array.IndexOf(valueFlags, argument) >= 0)
            {
                var value = index + 1 < arguments.Length ? arguments[index + 1].Trim() : string.Empty;

                index++;

                if (argument == ProfileFlag)
                {
                    profile = value.Length == 0 ? null : value;
                }
                else if (KindOf(argument) is { } kind)
                {
                    if (value.Length == 0)
                    {
                        error = $"{argument} needs a value";

                        return null;
                    }

                    targets.Add(new StartTarget(kind, value));
                }
            }
            else if (IsIssueKey(argument))
            {
                targets.Add(new StartTarget(StartTargetKind.Issue, JumpKey.Parse(argument, null).Key));
            }
        }

        if (targets.Count > 1)
        {
            error = "give one start target: an issue key, --board, --backlog, --jql or --filter";

            return null;
        }

        return new StartOptions(profile, targets.Count == 0 ? null : targets[0]);
    }

    private static StartTargetKind? KindOf(string flag)
    {
        return flag switch
        {
            BoardFlag => StartTargetKind.Board,
            BacklogFlag => StartTargetKind.Backlog,
            JqlFlag => StartTargetKind.Jql,
            FilterFlag => StartTargetKind.Filter,
            _ => null,
        };
    }
}
