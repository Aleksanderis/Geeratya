namespace Geeratya.Core.Lists;

/// <summary>
/// The state of every "choose one" list (<c>docs/internal/ui-consistency.md</c>, component 5): the source picker, Jump,
/// the palette, the context menu, the form, create-pane and dropdown popups, the comment <c>@</c> popup, the Settings
/// choice list, the type chooser and the profile list. It holds the highlight and the lookup status; the rows are
/// derived by each user from its own source (a typed filter goes through <see cref="Matcher"/>) and are passed in as
/// a count, so the state never owns a copy of them. Every move goes through <see cref="ListCursor"/>, so all users
/// share one clamp, one scroll margin and one half-page rule. Pure: no clock, no terminal.
/// </summary>
/// <param name="Selected">
/// The highlighted row; -1 means no highlight (a popup of quick rows that waits for the first arrow key). Read it
/// through <see cref="Index"/> against the row count, because the rows may have shrunk since.
/// </param>
/// <param name="Status">Where the lookup is.</param>
/// <param name="Message">Why the last lookup failed, or null.</param>
public readonly record struct PickList(int Selected = 0, PickStatus Status = PickStatus.Ready, string? Message = null)
{
    /// <summary>The highlight of a list that has just opened or whose rows were replaced: the first row.</summary>
    public static PickList First => new(0);

    /// <summary>A list that highlights nothing until the user moves.</summary>
    public static PickList Unhighlighted => new(-1);

    /// <summary>The highlight at <paramref name="index"/>, kept inside <paramref name="count"/> rows.</summary>
    public PickList At(int index, int count)
    {
        return this with { Selected = ListCursor.Clamp(index, count) };
    }

    /// <summary>
    /// The highlight after <paramref name="delta"/> rows, stopping at the first and the last row. With no highlight
    /// yet, any step lands on the first row.
    /// </summary>
    public PickList Moved(int delta, int count)
    {
        return this with { Selected = ListCursor.Step(Selected, delta, count) };
    }

    /// <summary>
    /// The highlight after a <see cref="ListMove"/> repeated <paramref name="times"/> times; a half page is half of
    /// <paramref name="viewport"/> rows.
    /// </summary>
    public PickList Moved(ListMove move, int times, int count, int viewport)
    {
        return this with
        {
            Selected = ListCursor.Target(Math.Max(0, Selected), move, times, count, ListCursor.Half(viewport)),
        };
    }

    /// <summary>The highlight clamped to <paramref name="count"/> rows (0 when there are none; -1 stays -1).</summary>
    public int Index(int count)
    {
        return Selected < 0 ? -1 : ListCursor.Clamp(Selected, count);
    }

    /// <summary>The rows to draw in a viewport of <paramref name="area"/> rows, the highlight kept in view.</summary>
    public PickWindow Window(int count, int area)
    {
        return WindowAt(Selected, count, area);
    }

    /// <summary>
    /// The rows to draw when the list has more display lines than rows (headers, gaps): <paramref name="line"/> is
    /// the display line of the highlight, <paramref name="count"/> the lines.
    /// </summary>
    public static PickWindow WindowAt(int line, int count, int area)
    {
        if (count <= 0 || area <= 0)
        {
            return new PickWindow(0, 0);
        }

        var first = ListCursor.FirstVisible(Math.Max(0, line), count, area);

        return new PickWindow(first, Math.Min(area, count - first));
    }

    /// <summary>The list waiting for a reply: the rows stay, the highlight stays, nothing is said yet.</summary>
    public PickList Waiting()
    {
        return this with { Status = PickStatus.Waiting, Message = null };
    }

    /// <summary>The list marked as searching (the lookup is slow).</summary>
    public PickList Loading()
    {
        return this with { Status = PickStatus.Loading };
    }

    /// <summary>The list with its reply in: ready, no error.</summary>
    public PickList Ready()
    {
        return this with { Status = PickStatus.Ready, Message = null };
    }

    /// <summary>The list after a failed lookup, still showing what is known.</summary>
    public PickList Failed(string message)
    {
        return this with { Status = PickStatus.Failed, Message = message };
    }
}
