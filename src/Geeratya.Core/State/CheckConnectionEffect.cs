using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>Verify the credentials of <paramref name="Profile"/> with <c>GET /myself</c>; answered by <see cref="ConnectionChecked"/> or <see cref="ConnectionCheckFailed"/>.</summary>
public sealed record CheckConnectionEffect(Profile Profile) : Effect;
