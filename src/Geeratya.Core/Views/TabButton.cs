namespace Geeratya.Core.Views;

/// <summary>
/// The cells of the <c>+</c> button in the tab bar: <paramref name="Start"/> inclusive, <paramref name="End"/> exclusive.
/// </summary>
public readonly record struct TabButton(int Start, int End);
