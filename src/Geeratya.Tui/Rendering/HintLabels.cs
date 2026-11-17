namespace Geeratya.Tui.Rendering;

/// <summary>
/// The one label table of every dialog footer: one word per action (<c>docs/internal/ui-consistency.md</c>,
/// component 6). <see cref="Close"/> is Esc leaving a dialog; Enter reads <see cref="Open"/> (it opens a thing),
/// <see cref="Choose"/> (it takes a value into a field),
/// <see cref="Switch"/>, <see cref="Run"/>, <see cref="Save"/> or <see cref="Send"/>. A new footer takes its word from
/// here, so no dialog invents a synonym.
/// </summary>
public static class HintLabels
{
    /// <summary>Esc leaves the dialog.</summary>
    public const string Close = "close";

    /// <summary>Esc empties the typed filter first.</summary>
    public const string ClearFilter = "clear filter";

    /// <summary>The answer that returns to a form that asked to discard.</summary>
    public const string KeepEditing = "keep editing";

    /// <summary>The answer that throws the edit away.</summary>
    public const string Discard = "discard";

    /// <summary>Up and Down move the highlight.</summary>
    public const string Move = "move";

    /// <summary>Enter opens the highlighted thing (an issue, a board, a view).</summary>
    public const string Open = "open";

    /// <summary>Enter takes the highlighted value into the field.</summary>
    public const string Choose = "choose";

    /// <summary>Enter switches to the highlighted tab.</summary>
    public const string Switch = "switch";

    /// <summary>Enter runs the highlighted action.</summary>
    public const string Run = "run";

    /// <summary>Enter writes the form.</summary>
    public const string Save = "save";

    /// <summary>Enter writes a new comment.</summary>
    public const string Send = "send";

    /// <summary>Enter moves the marked issues to the highlighted sprint.</summary>
    public const string MoveIssues = "move";

    /// <summary>Enter starts a new line.</summary>
    public const string NewLine = "new line";

    /// <summary>Tab moves to the next field.</summary>
    public const string Field = "field";

    /// <summary>Opens the link in the browser.</summary>
    public const string Browser = "browser";

    /// <summary>Opens the highlighted row in a new tab.</summary>
    public const string NewTab = "new tab";

    /// <summary>Enter opens the highlighted board, filter or query in a new tab.</summary>
    public const string OpenInNewTab = "open in a new tab";

    /// <summary>Tests the connection of a profile.</summary>
    public const string Test = "test";

    /// <summary>Changes the highlighted setting.</summary>
    public const string Change = "change";

    /// <summary>Keeps the highlighted choice.</summary>
    public const string Keep = "keep";

    /// <summary>Resets the highlighted setting.</summary>
    public const string Reset = "reset";
}
