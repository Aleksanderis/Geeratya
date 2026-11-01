using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// Cancel the in-flight work of a tab (it was closed or gave up its rows). The host calls
/// <c>NetworkRunner.CancelScope</c>.
/// </summary>
public sealed record CancelTabEffect(TabId Tab) : Effect;
