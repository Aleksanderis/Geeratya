using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Core.Forms;

/// <summary>
/// How every form shows an error, in all four presentations: the message wrapped to the width (Jira's own sentence
/// stays readable, never cut mid-sentence), the first row starting with <c>! </c>, at most
/// <see cref="MaxRows"/> rows, in the error role. A field's error goes under the field, an error that belongs to no
/// field (a load failure, a refused write) at the top of the form. Pure.
/// </summary>
public static class FormErrors
{
    /// <summary>The most rows one error takes.</summary>
    public const int MaxRows = 6;

    private const string Mark = "! ";
    private const string Hang = "  ";

    /// <summary>The rows of <paramref name="message"/> for a content width of <paramref name="width"/> columns.</summary>
    public static List<StyledLine> Lines(string message, int width)
    {
        var room = Math.Max(1, width - Mark.Length);
        var rows = ConfirmDialog.Wrap(message, room).Take(MaxRows).ToList();

        return [.. rows.Select((row, index) => StyledLine.Of($"{(index == 0 ? Mark : Hang)}{row}", TextRole.Error))];
    }
}
