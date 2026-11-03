namespace Geeratya.Core.State;

/// <summary>
/// One notice of the status line. <paramref name="PostedAt"/> is null until the first command that carries a time (a
/// tick or a key press) stamps it: the reducer has no wall clock, so a lifetime counts from that injected time.
/// </summary>
public sealed record StatusMessage(string Text, StatusSeverity Severity, DateTimeOffset? PostedAt = null)
{
    /// <summary>The text with its severity prefix, so the level never rests on colour alone.</summary>
    public string Line => StatusMessages.PrefixOf(Severity) + Text;
}
