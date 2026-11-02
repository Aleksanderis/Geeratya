namespace Geeratya.Core.State;

/// <summary>
/// Open a link in the system browser. Only the reducer creates it, after <see cref="Browser.BrowserUrlCheck"/>;
/// the host answers with <see cref="UrlOpenFinished"/>.
/// </summary>
public sealed record OpenUrlEffect(Uri Url, string? Label = null) : Effect;
