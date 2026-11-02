using System.Text.Json.Serialization;

namespace Geeratya.Jira.Tests.Live;

/// <summary>The few reply shapes the live write round trip reads; the write clients bring their own later.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LiveCreatedIssue))]
[JsonSerializable(typeof(LiveCreatedComment))]
[JsonSerializable(typeof(LiveIssueState))]
[JsonSerializable(typeof(LiveTransitionList))]
[JsonSerializable(typeof(LiveDoneState))]
public sealed partial class LiveWriteJsonContext : JsonSerializerContext;

public sealed record LiveCreatedIssue(string Id, string Key);

public sealed record LiveCreatedComment(string Id);

public sealed record LiveIssueState(LiveIssueFields Fields);

public sealed record LiveIssueFields(string Summary, LiveStatus Status, IReadOnlyList<string> Labels);

public sealed record LiveStatus(string Name);

public sealed record LiveTransitionList(IReadOnlyList<LiveTransition> Transitions);

public sealed record LiveTransition(string Id, LiveStatus To);

public sealed record LiveDoneState(LiveDoneFields Fields);

public sealed record LiveDoneFields(LiveStatus Status, LiveNamed? Resolution, IReadOnlyList<LiveNamed> Components);

public sealed record LiveNamed(string Name);
