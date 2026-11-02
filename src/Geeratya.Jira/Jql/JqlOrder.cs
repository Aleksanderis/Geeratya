namespace Geeratya.Jira.Jql;

/// <summary>One <c>ORDER BY</c> term: a field name and a direction.</summary>
/// <param name="Field">A JQL field name (bare, so only names this app defines, never user input).</param>
/// <param name="Descending">True for <c>DESC</c>, false for <c>ASC</c>.</param>
public sealed record JqlOrder(string Field, bool Descending = false)
{
    public static readonly JqlOrder RankAscending = new(JqlBuilder.RankField);
    public static readonly JqlOrder CreatedAscending = new(JqlBuilder.CreatedField);
    public static readonly JqlOrder CreatedDescending = new(JqlBuilder.CreatedField, true);
    public static readonly JqlOrder UpdatedAscending = new(JqlBuilder.UpdatedField);
    public static readonly JqlOrder UpdatedDescending = new(JqlBuilder.UpdatedField, true);
}
