using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>An input to the reducer.</summary>
public abstract record Command;

/// <summary>
/// A key in <see cref="Geeratya.Core.Keymap.KeyNotation"/> spelling, with the time it was pressed (the reducer reads
/// no clock; a submitted form needs the time for its write).
/// </summary>
public sealed record KeyPressed(string Key, DateTimeOffset At = default) : Command;

/// <summary>Opens Confirm mode for a destructive action.</summary>
public sealed record ConfirmRequested(ConfirmRequest Request) : Command;

/// <summary>Text pasted by the terminal (bracketed paste); fills the active Insert or Command field.</summary>
public sealed record TextPasted(string Text) : Command;

/// <summary>
/// An action id that a non-key input (the mouse table) asks for: it runs the handler the key would, repeated
/// <paramref name="Count"/> times for motions.
/// </summary>
public sealed record ActionRequested(string Action, int Count = 1, DateTimeOffset At = default) : Command;

/// <summary>
/// A click that places the cursor: the pointer does what motions do for a key (<c>docs/internal/ux-mouse.md</c>). On a list it
/// selects the row, on columns the card or the column header, in an opened issue the line.
/// </summary>
/// <param name="Column">The screen column of the click.</param>
/// <param name="Row">The row inside the body (the first row under the tab bar and banner is 0).</param>
/// <param name="Width">The screen width the body was drawn for; columns are laid out from it.</param>
/// <param name="Then">
/// The action to run on what was placed (double-click opens, right-click opens the palette); it runs only when the
/// click landed on a row, card or line, so a click on empty space never acts on the old selection.
/// </param>
public sealed record PointerPlaced(int Column, int Row, int Width, ActionRequested? Then = null) : Command;

/// <summary>
/// The wheel turned over the opened issue: the pane under the pointer takes the focus (when it is not the one that
/// holds the cursor), then <paramref name="Action"/> (a motion repeated 3 times) scrolls it.
/// </summary>
/// <param name="Column">The screen column of the pointer.</param>
/// <param name="Row">The row inside the body (the first row under the tab bar and banner is 0).</param>
/// <param name="Action">The motion the notch runs.</param>
public sealed record DetailWheeled(int Column, int Row, ActionRequested Action) : Command;

/// <summary>
/// The pointer moved over the tab bar (or off it): the chip under it, null when none. The chip shows its close
/// <c>x</c> while hovered (<c>docs/internal/ux-look.md</c>, section 5); it changes nothing else and has no action id.
/// </summary>
public sealed record TabHovered(TabId? Tab) : Command;

/// <summary>
/// A card dragged from one cell to another on the columns layout (press, move, release). It is the key
/// <c>issue.transition-next</c> or <c>issue.transition-previous</c> for the card under the first cell, so the same
/// rules apply (a single matching transition is sent, otherwise the picker or its form opens); only a drop on the
/// neighbouring column acts, anything else does nothing and says so.
/// </summary>
/// <param name="FromColumn">The screen column where the button went down.</param>
/// <param name="FromRow">The body row where the button went down.</param>
/// <param name="ToColumn">The screen column where it was released.</param>
/// <param name="Width">The screen width the body was drawn for.</param>
public sealed record CardDragged(int FromColumn, int FromRow, int ToColumn, int Width)
    : Command;

/// <summary>
/// Rows dragged over with the pointer on the list or backlog layout (press, move, release): Visual mode from the
/// first row to the last, the same as <c>v</c> and the motions. On a Scrum backlog a release inside another section
/// asks to move the issues there instead.
/// </summary>
/// <param name="Column">The screen column where the button went down.</param>
/// <param name="FromRow">The body row where the button went down.</param>
/// <param name="ToRow">The body row where it was released.</param>
/// <param name="Width">The screen width the body was drawn for.</param>
/// <param name="At">When it was released.</param>
/// <param name="Live">The range was already built while the button was held (<see cref="RowsDragging"/>), so the
/// release only moves the cursor; the press row may have scrolled away.</param>
public sealed record RowsDragged(
    int Column,
    int FromRow,
    int ToRow,
    int Width,
    DateTimeOffset At = default,
    bool Live = false) : Command;

/// <summary>
/// The held button moved over a list or backlog row: Visual mode starts on the pressed row at the first move and the
/// cursor follows the pointer, so the range shows while the button is down. On a Scrum backlog the cursor stays inside
/// the section of the pressed row (a drop on another section is asked for on release).
/// </summary>
/// <param name="Column">The screen column where the button went down.</param>
/// <param name="FromRow">The body row where the button went down.</param>
/// <param name="ToRow">The body row under the pointer.</param>
/// <param name="Width">The screen width the body was drawn for.</param>
/// <param name="First">The first move of this drag: the range starts on the pressed row.</param>
public sealed record RowsDragging(int Column, int FromRow, int ToRow, int Width, bool First) : Command;

/// <summary>
/// One auto-scroll step of a drag whose pointer rests outside the list body: the cursor moves by that many rows (the
/// view scrolls with it) and the range follows. Nothing moves at either end of the list.
/// </summary>
/// <param name="Rows">Rows to move, negative upwards.</param>
public sealed record RowsDragScrolled(int Rows) : Command;
