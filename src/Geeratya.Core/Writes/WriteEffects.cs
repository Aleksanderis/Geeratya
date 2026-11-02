using Geeratya.Core.State;

namespace Geeratya.Core.Writes;

/// <summary>
/// Send the write. The handler runs it on a scope of its own (the operation id), so closing a tab never cancels
/// it, and answers with <c>WriteSucceeded</c>, <c>WriteFailed</c> or <c>WriteOutcomeUnknown</c>.
/// </summary>
public sealed record ExecuteWriteEffect(long OperationId, WriteRequest Request) : Effect;

/// <summary>Read the written issues again after a lost reply and answer with <c>WriteVerified</c>.</summary>
public sealed record VerifyWriteEffect(long OperationId, WriteRequest Request) : Effect;

/// <summary>
/// Drop the cached detail of the issues and the first-page entries of every view of the profile (the app cannot
/// tell which cached pages contain the issue).
/// </summary>
public sealed record InvalidateCacheEffect(IReadOnlyList<string> Keys) : Effect;
