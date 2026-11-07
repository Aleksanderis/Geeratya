namespace Geeratya.Core.Forms;

/// <summary>
/// Which parents an issue type accepts. Jira's hierarchy is by level (epic 1, standard issue types 0, sub-tasks -1,
/// more levels above the epic with Advanced Roadmaps): a parent is always the level directly above, a sub-task's is a
/// standard issue (level 0). The same rule holds for company-managed and team-managed projects.
/// </summary>
public static class ParentLevels
{
    private const int StandardLevel = 0;
    private const int SubtaskLevel = -1;

    /// <summary>The level of an accepted parent, or null when the type's level is unknown (nothing is narrowed).</summary>
    /// <param name="level">The type's hierarchy level.</param>
    /// <param name="isSubtask">True for a sub-task type.</param>
    public static int? Accepted(int level, bool isSubtask)
    {
        return isSubtask || level <= SubtaskLevel ? StandardLevel : level + 1;
    }
}
