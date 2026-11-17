namespace Geeratya.Core.Text;

/// <summary>The words for a value that is not set, the same on every surface: people read "Unassigned", everything else "None".</summary>
public static class EmptyValues
{
    /// <summary>An empty person field (Assignee, Reporter).</summary>
    public const string Unassigned = "Unassigned";

    /// <summary>Any other empty value (labels, due date, priority, estimate).</summary>
    public const string None = "None";
}
