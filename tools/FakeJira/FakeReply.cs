namespace Geeratya.FakeJira;

/// <summary>One reply of the fake: status, JSON body and an optional Retry-After.</summary>
public sealed record FakeReply(int Status, byte[] Body, int? RetryAfterSeconds = null);
