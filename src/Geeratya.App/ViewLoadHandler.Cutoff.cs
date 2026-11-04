using Geeratya.Core.Boards;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

public sealed partial class ViewLoadHandler
{
    /// <summary>
    /// The Done cutoff a Kanban board load applies. Later pages repeat the first page's value (#699). The first page
    /// takes the tab's quick choice, else Jira's own value (read through the gateway, cached for the connection), else the <c>board.done-cutoff</c> setting. Jira's answer is optional: a
    /// failed, slow, malformed or refused read falls back to the setting without a message and never fails the load.
    /// </summary>
    private static async Task<AppliedCutoff> ResolveCutoffAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long boardId,
        CancellationToken token)
    {
        if (effect.PinnedCutoff is { } pinned)
        {
            return new AppliedCutoff(pinned.Days, pinned.Source);
        }

        if (effect.DoneCutoffChoice is { } choice)
        {
            return new AppliedCutoff(choice, DoneCutoffSource.Choice);
        }

        var priority = effect.Background ? JiraPriority.Background : JiraPriority.User;

        try
        {
            var read = await services.DoneCutoffs.ReadAsync(boardId, token, priority);

            if (read.IsSuccess)
            {
                return new AppliedCutoff(read.Value.Days ?? DoneCutoff.AllDays, DoneCutoffSource.Jira);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Optional lookup (see the summary): the setting applies.
        }

        return new AppliedCutoff(effect.DoneCutoffDays, DoneCutoffSource.Setting);
    }

    /// <summary>The days a Kanban load applies (0 for none) and where they came from.</summary>
    private sealed record AppliedCutoff(int Days, DoneCutoffSource Source);
}
