namespace Geeratya.FakeJira;

/// <summary>
/// One reply of the fake: status, JSON body and an optional Retry-After. <paramref name="DropConnection"/> closes the
/// connection without sending the reply (the request was read, and a write was already applied).
/// </summary>
public sealed record FakeReply(int Status, byte[] Body, int? RetryAfterSeconds = null, bool DropConnection = false);
