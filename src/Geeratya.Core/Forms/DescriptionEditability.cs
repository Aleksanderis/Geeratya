using System.Runtime.CompilerServices;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Forms;

/// <summary>
/// Whether a description can be edited as Markdown-like text without losing content (<see cref="AdfRoundTrip"/>). The
/// answer is remembered per document, because the detail asks on every layout and the check parses the text twice.
/// </summary>
public static class DescriptionEditability
{
    private static readonly ConditionalWeakTable<AdfDocument, object> _answers = [];
    private static readonly object _yes = new();
    private static readonly object _no = new();

    /// <summary>True when the document survives the text round trip (a missing description is editable).</summary>
    public static bool CanEdit(AdfDocument? document)
    {
        if (document is null)
        {
            return true;
        }

        var answer = _answers.GetValue(document, static current => AdfRoundTrip.RoundTrips(current) ? _yes : _no);

        return ReferenceEquals(answer, _yes);
    }
}
