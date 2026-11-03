using System.Globalization;

namespace Geeratya.Core.Views;

/// <summary>
/// The value of a <see cref="ViewSourceKind.Sprint"/> view: a Scrum board and either one sprint or the active ones.
/// Written <c>boardId:sprintId</c> or <c>boardId:active</c>; the text is the cache key and the load handler's input,
/// so it must stay stable.
/// </summary>
/// <param name="BoardId">The Scrum board.</param>
/// <param name="SprintId">The sprint, null for the active sprint(s) of the board.</param>
public sealed record SprintSource(long BoardId, long? SprintId)
{
    private const string ActiveText = "active";

    /// <summary>The board's active sprint(s).</summary>
    public static SprintSource Active(long boardId)
    {
        return new SprintSource(boardId, null);
    }

    /// <summary>Reads a source value; false for anything that is not exactly the written form.</summary>
    public static bool TryParse(string value, out SprintSource source)
    {
        source = new SprintSource(0, null);
        var parts = value.Split(':');

        if (parts.Length != 2 || !TryNumber(parts[0], out var board))
        {
            return false;
        }

        if (parts[1] == ActiveText)
        {
            source = Active(board);

            return true;
        }

        if (!TryNumber(parts[1], out var sprint))
        {
            return false;
        }

        source = new SprintSource(board, sprint);

        return true;
    }

    public override string ToString()
    {
        var sprint = SprintId is { } id ? id.ToString(CultureInfo.InvariantCulture) : ActiveText;

        return $"{BoardId.ToString(CultureInfo.InvariantCulture)}:{sprint}";
    }

    private static bool TryNumber(string text, out long number)
    {
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
