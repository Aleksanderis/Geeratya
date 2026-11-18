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

    /// <summary>Esc leaves the editing of a form field for the navigate state.</summary>
    public const string StopEditing = "stop editing";

    /// <summary>Esc empties the typed filter first.</summary>
    public const string ClearFilter = "clear filter";

    /// <summary>Enter keeps the typed filter and leaves the filter line.</summary>
    public const string KeepFilter = "keep filter";

    /// <summary>Enter starts editing the focused field of a form in its navigate state.</summary>
    public const string Edit = "edit";

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

    /// <summary>Ctrl-s writes a new issue (the form of the create panes).</summary>
    public const string Create = "create";

    /// <summary>Enter moves to the next field of the profile form.</summary>
    public const string Next = "next";

    /// <summary>Starts a new profile.</summary>
    public const string Add = "add";

    /// <summary>Removes the highlighted profile.</summary>
    public const string Remove = "remove";

    /// <summary>Enter jumps to the nearest flash target.</summary>
    public const string Nearest = "nearest";

    /// <summary>Moves the focus between the panes of the create form.</summary>
    public const string Pane = "pane";

    /// <summary>Folds the more fields of the create form in or out.</summary>
    public const string MoreFields = "more fields";

    /// <summary>Enter writes a new comment.</summary>
    public const string Send = "send";

    /// <summary>Enter moves the marked issues to the highlighted sprint.</summary>
    public const string MoveIssues = "move here";

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

    /// <summary>Tab shows the next page of the Help.</summary>
    public const string Page = "page";

    /// <summary>Starts the filter of the Help.</summary>
    public const string Filter = "filter";

    /// <summary>Esc drops the half-typed key sequence, or cancels a form (the question about changes comes first).</summary>
    public const string Cancel = "cancel";
}
