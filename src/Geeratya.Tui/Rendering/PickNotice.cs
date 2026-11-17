using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The line under the rows of a pick list that says what is going on (searching, failed, nothing to show).</summary>
/// <param name="Text">The words.</param>
/// <param name="Role">The text role they are drawn in.</param>
public sealed record PickNotice(string Text, TextRole Role);
