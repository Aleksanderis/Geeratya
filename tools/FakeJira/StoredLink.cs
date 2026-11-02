namespace Geeratya.FakeJira;

/// <summary>One link as an issue lists it: the other issue, and which wording of the type this issue reads.</summary>
public sealed record StoredLink(
    string Id,
    string TypeId,
    string TypeName,
    string Inward,
    string Outward,
    string OtherKey,
    bool ReadsOutward);
