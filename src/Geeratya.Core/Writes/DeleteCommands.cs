using Geeratya.Core.State;

namespace Geeratya.Core.Writes;

/// <summary>
/// The sub-tasks of the issue were counted (the question of the delete confirm). <paramref name="More"/> is true
/// when there are more than the count shows. Ignored unless it answers the pending question.
/// </summary>
public sealed record SubtasksCounted(long RequestId, int Count, bool More, DateTimeOffset At) : WriteCommand;

/// <summary>The sub-tasks could not be counted, so nothing is asked and nothing is deleted.</summary>
public sealed record SubtasksCountFailed(long RequestId, string Reason) : WriteCommand;

/// <summary>Count the sub-tasks of an issue (a read) and answer with <see cref="SubtasksCounted"/> or a failure.</summary>
public sealed record CountSubtasksEffect(long RequestId, string Key) : Effect;
